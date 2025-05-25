using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Configuration.Settings;
using PKISharp.WACS.Configuration.Settings.Csr;
using PKISharp.WACS.Configuration.Settings.Store;
using PKISharp.WACS.Extensions;
using System;
using System.IO;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using static System.Environment;

namespace PKISharp.WACS.Services
{
    public class SettingsService
    {
        private readonly ILogService _log;
        private readonly MainArguments? _arguments;
        private readonly InheritSettings _settings = new();
        public ISettings Settings => _settings;

        public SettingsService(ILogService log, ArgumentsParser parser)
        {
            _log = log;

            // Validate command line and ensure main arguments
            // are loaded, because those influence the BaseUri
            if (!parser.Validate())
            {
                return;
            }
            _arguments = parser.GetArguments<MainArguments>();
            if (_arguments == null)
            {
                return;
            }

            var globalFile = EnsureGlobalSettingsFile();
            try
            {
                var globalSettings = Load(globalFile);
                _settings = new InheritSettings(globalSettings);
            }
            catch (Exception ex)
            {
                _log.Error($"Unable to start program using {globalFile.Name}");
                while (ex.InnerException != null)
                {
                    _log.Error(ex.InnerException.Message);
                    ex = ex.InnerException;
                }
                return;
            }

            try
            {
                _settings.BaseUri = ChooseBaseUri();
            }
            catch
            {
                _log.Error("Error choosing ACME server");
                return;
            }

            try
            {
                EnsureFolderExists(_settings.Client.ConfigRoot, "configuration", true);
                EnsureFolderExists(_settings.Client.ConfigurationPath, "configuration", false);
                var pathCompareMode = 
                    OperatingSystem.IsWindows() ? 
                    StringComparison.OrdinalIgnoreCase : 
                    StringComparison.Ordinal;
                EnsureFolderExists(_settings.Client.LogPath, "log", !_settings.Client.LogPath.StartsWith(Settings.Client.ConfigurationPath, pathCompareMode));
                EnsureFolderExists(_settings.Cache.CachePath, "cache", !_settings.Client.LogPath.StartsWith(Settings.Client.ConfigurationPath, pathCompareMode));

                // Configure disk logger
                _log.ApplyClientSettings(Settings.Client);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error initializing program");
                return;
            }
            _settings.Valid = true;
        }

        private FileInfo EnsureGlobalSettingsFile()
        {
            var settingsFileName = "settings.json";
            var settingsFileTemplateName = "settings_default.json";
            _log.Verbose("Looking for {settingsFileName} in {path}", settingsFileName, VersionService.SettingsPath);
            var settings = new FileInfo(Path.Combine(VersionService.SettingsPath, settingsFileName));
            var settingsTemplate = new FileInfo(Path.Combine(VersionService.ResourcePath, settingsFileTemplateName));
            var useFile = settings;
            if (!settings.Exists)
            {
                if (!settingsTemplate.Exists)
                {
                    // For .NET tool case
                    settingsTemplate = new FileInfo(Path.Combine(VersionService.ResourcePath, settingsFileName));
                }
                if (!settingsTemplate.Exists)
                {
                    _log.Warning("Unable to locate {settings}", settingsFileName);
                }
                else
                {
                    _log.Verbose("Copying {settingsFileTemplateName} to {settingsFileName}", settingsFileTemplateName, settingsFileName);
                    try
                    {
                        if (!settings.Directory!.Exists)
                        {
                            settings.Directory.Create();
                        }
                        settingsTemplate.CopyTo(settings.FullName);
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, "Unable to create {settingsFileName}, falling back to defaults", settingsFileName);
                        useFile = settingsTemplate;
                    }
                }
            }
            return useFile;
        }

        /// <summary>
        /// Load settings from disk
        /// </summary>
        /// <param name="useFile"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private Settings Load(FileInfo useFile)
        {
            using var fs = useFile.OpenRead();
            var newSettings = JsonSerializer.Deserialize(fs, SettingsJson.Insensitive.Settings) ?? throw new Exception($"Unable to deserialize {useFile.FullName}");

            // Migrate old-style settings to new-style settings
            newSettings.Source.DefaultSource ??= newSettings.Target.DefaultTarget;
            if (newSettings.Store.DefaultPemFilesPath != null)
            {
                newSettings.Store.PemFiles ??= new PemFilesSettings();
                newSettings.Store.PemFiles.DefaultPath ??= newSettings.Store.DefaultPemFilesPath;
            }
            if (newSettings.Store.DefaultCentralSslStore != null)
            {
                newSettings.Store.CentralSsl ??= new CentralSslSettings();
                newSettings.Store.CentralSsl.DefaultPath ??= newSettings.Store.DefaultCentralSslStore;
            }
            if (newSettings.Store.DefaultCentralSslPfxPassword != null)
            {
                newSettings.Store.CentralSsl ??= new CentralSslSettings();
                newSettings.Store.CentralSsl.DefaultPassword ??= newSettings.Store.DefaultCentralSslPfxPassword;
            }
            if (newSettings.Store.DefaultCertificateStore != null)
            {
                newSettings.Store.CertificateStore ??= new CertificateStoreSettings();
                newSettings.Store.CertificateStore.DefaultStore ??= newSettings.Store.DefaultCertificateStore;
            }
            if (newSettings.Security.ECCurve != null)
            {
                newSettings.Csr.Ec ??= new EcSettings();
                newSettings.Csr.Ec.CurveName ??= newSettings.Security.ECCurve;
            }
            if (newSettings.Security.PrivateKeyExportable != null)
            {
                newSettings.Store.CertificateStore ??= new CertificateStoreSettings();
                newSettings.Store.CertificateStore.PrivateKeyExportable ??= newSettings.Security.PrivateKeyExportable;
            }
            if (newSettings.Security.RSAKeyBits != null)
            {
                newSettings.Csr.Rsa ??= new RsaSettings();
                newSettings.Csr.Rsa.KeyBits ??= newSettings.Security.RSAKeyBits;
            }
            return newSettings;
        }

        /// <summary>
        /// Choose the base URI based on command line options and/or global settings defaults
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private Uri ChooseBaseUri()
        {
            if (!string.IsNullOrWhiteSpace(_arguments?.BaseUri))
            {
                try
                {
                    return new Uri(_arguments.BaseUri);
                } 
                catch (Exception ex)
                {
                    _log.Error(ex, "Invalid --baseuri specified");
                    throw;
                }
            }
            if (_arguments?.Test ?? false)
            {
                if (Settings.Acme.DefaultBaseUriTest?.IsAbsoluteUri ?? false)
                {
                    return Settings.Acme.DefaultBaseUriTest;
                } 
                else
                {
                    _log.Warning("Setting Acme.DefaultBaseUriTest is unspecified or invalid, fallback to Acme.DefaultBaseUri");
                }
            }
            if (Settings.Acme.DefaultBaseUri?.IsAbsoluteUri ?? false)
            {
                return Settings.Acme.DefaultBaseUri;
            }
            else
            {
                _log.Error("Setting Acme.DefaultBaseUri is unspecified or invalid, please specify a valid absolute URI");
                throw new Exception();
            }
        }

        /// <summary>
        /// Create folder if needed
        /// </summary>
        /// <param name="path"></param>
        /// <param name="label"></param>
        /// <exception cref="Exception"></exception>
        private void EnsureFolderExists(string path, string label, bool checkAcl)
        {
            var created = false;
            var di = new DirectoryInfo(path);
            if (!di.Exists)
            {
                try
                {
                    di = Directory.CreateDirectory(path);
                    _log.Debug($"Created {label} folder {{path}}", path);
                    created = true;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Unable to create {label} {path}", ex);
                }
            }
            else
            {
                _log.Debug($"Use existing {label} folder {{path}}", path);
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
        private void EnsureFolderAclLinux(DirectoryInfo di, string label, bool created) {
            var currentMode = File.GetUnixFileMode(di.FullName);
            if (currentMode.HasFlag(UnixFileMode.OtherRead) || 
                currentMode.HasFlag(UnixFileMode.OtherExecute) ||
                currentMode.HasFlag(UnixFileMode.OtherWrite))
            {
                if (!created)
                {
                    _log.Warning("All users currently have access to {path}.", di.FullName);
                    _log.Warning("We will now try to limit access to improve security...", label, di.FullName);
                }
                var newMode = currentMode & ~(UnixFileMode.OtherRead | UnixFileMode.OtherExecute | UnixFileMode.OtherWrite);
                _log.Warning("Change file mode in {label} to {newMode}", label, newMode);
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
                _log.Warning("All users currently have access to {path}.", di.FullName);
                _log.Warning("We will now try to limit access to improve security...", label, di.FullName);
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
                _log.Warning($"...done. You may manually add specific trusted accounts to the ACL.");
            } 
            catch (Exception ex)
            {
                _log.Error(ex, $"...failed, please take this step manually.");
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