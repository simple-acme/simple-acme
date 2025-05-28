using PKISharp.WACS.Services;
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace PKISharp.WACS.Configuration.Settings
{
    internal class FolderHelpers(ILogService log)
    {
        /// <summary>
        /// Create folder if needed
        /// </summary>
        /// <param name="path"></param>
        /// <param name="label"></param>
        /// <exception cref="Exception"></exception>
        public void EnsureFolderExists(string path, string label, bool checkAcl)
        {
            var created = false;
            var di = new DirectoryInfo(path);
            if (!di.Exists)
            {
                try
                {
                    di = Directory.CreateDirectory(path);
                    log.Debug($"Created {label} folder {{path}}", path);
                    created = true;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Unable to create {label} {path}", ex);
                }
            }
            else
            {
                log.Debug($"Use existing {label} folder {{path}}", path);
            }
            if (checkAcl)
            {
                if (OperatingSystem.IsWindows())
                {
                    EnsureFolderAcl(di, label, created);
                }
                else if (OperatingSystem.IsLinux())
                {
                    EnsureFolderAclLinux(di, label, created);
                }

            }
        }

        [SupportedOSPlatform("linux")]
        private void EnsureFolderAclLinux(DirectoryInfo di, string label, bool created)
        {
            var currentMode = File.GetUnixFileMode(di.FullName);
            if (currentMode.HasFlag(UnixFileMode.OtherRead) ||
                currentMode.HasFlag(UnixFileMode.OtherExecute) ||
                currentMode.HasFlag(UnixFileMode.OtherWrite))
            {
                if (!created)
                {
                    log.Warning("All users currently have access to {path}.", di.FullName);
                    log.Warning("We will now try to limit access to improve security...", label, di.FullName);
                }
                var newMode = currentMode & ~(UnixFileMode.OtherRead | UnixFileMode.OtherExecute | UnixFileMode.OtherWrite);
                log.Warning("Change file mode in {label} to {newMode}", label, newMode);
                File.SetUnixFileMode(di.FullName, newMode);
            }
        }

        /// <summary>
        /// Ensure proper access rights to a folder
        /// </summary>
        [SupportedOSPlatform("windows")]
        private void EnsureFolderAcl(DirectoryInfo di, string label, bool created)
        {
            // Test access control rules
            var (access, inherited) = UsersHaveAccess(di);
            if (!access)
            {
                return;
            }

            if (!created)
            {
                log.Warning("All users currently have access to {path}.", di.FullName);
                log.Warning("We will now try to limit access to improve security...", label, di.FullName);
            }
            try
            {
                var acl = di.GetAccessControl();
                if (inherited)
                {
                    // Disable access rule inheritance
                    acl.SetAccessRuleProtection(true, true);
                    di.SetAccessControl(acl);
                    acl = di.GetAccessControl();
                }

                var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                var rules = acl.GetAccessRules(true, true, typeof(SecurityIdentifier));
                foreach (FileSystemAccessRule rule in rules)
                {
                    if (rule.IdentityReference == sid &&
                        rule.AccessControlType == AccessControlType.Allow)
                    {
                        acl.RemoveAccessRule(rule);
                    }
                }
                var user = WindowsIdentity.GetCurrent().User;
                if (user != null)
                {
                    // Allow user access from non-privilegdes perspective 
                    // as well.
                    acl.AddAccessRule(
                        new FileSystemAccessRule(
                            user,
                            FileSystemRights.Read | FileSystemRights.Delete | FileSystemRights.Modify,
                            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                            PropagationFlags.None,
                            AccessControlType.Allow));
                }
                di.SetAccessControl(acl);
                log.Warning($"...done. You may manually add specific trusted accounts to the ACL.");
            }
            catch (Exception ex)
            {
                log.Error(ex, $"...failed, please take this step manually.");
            }
        }

        /// <summary>
        /// Test if users have access through inherited or direct rules
        /// </summary>
        /// <param name="di"></param>
        /// <returns></returns>
        [SupportedOSPlatform("windows")]
        private static (bool, bool) UsersHaveAccess(DirectoryInfo di)
        {
            var acl = di.GetAccessControl();
            var sid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
            var rules = acl.GetAccessRules(true, true, typeof(SecurityIdentifier));
            var hit = false;
            var inherited = false;
            foreach (FileSystemAccessRule rule in rules)
            {
                if (rule.IdentityReference == sid &&
                    rule.AccessControlType == AccessControlType.Allow)
                {
                    hit = true;
                    inherited = inherited || rule.IsInherited;
                }
            }
            return (hit, inherited);
        }
    }
}
