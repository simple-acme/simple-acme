using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Configuration.Settings;
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
        private readonly Settings _settings;
        public ISettings Settings => _settings;

        public SettingsService(ILogService log, ArgumentsParser parser)
        {
            _log = log;
            _settings = new Settings();
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

            try
            {
                using var fs = useFile.OpenRead();
                var newSettings = JsonSerializer.Deserialize(fs, SettingsJson.Insensitive.Settings);
                if (newSettings != null)
                {
                    _settings = newSettings;
                }

                static string? Fallback(string? x, string? y) => x ?? y;
                _settings.Source.DefaultSource = Fallback(Settings.Source.DefaultSource, _settings.Target.DefaultTarget);
                _settings.Store.PemFiles.DefaultPath = Fallback(Settings.Store.PemFiles.DefaultPath, _settings.Store.DefaultPemFilesPath);
                _settings.Store.CentralSsl.DefaultPath = Fallback(Settings.Store.CentralSsl.DefaultPath, _settings.Store.DefaultCentralSslStore);
                _settings.Store.CentralSsl.DefaultPassword = Fallback(Settings.Store.CentralSsl.DefaultPassword, _settings.Store.DefaultCentralSslPfxPassword);
                _settings.Store.CertificateStore.DefaultStore = Fallback(Settings.Store.CertificateStore.DefaultStore, _settings.Store.DefaultCertificateStore);
            }
            catch (Exception ex)
            {
                _log.Error($"Unable to start program using {useFile.Name}");
                while (ex.InnerException != null)
                {
                    _log.Error(ex.InnerException.Message);
                    ex = ex.InnerException;
                }
                return;
            }

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
            try
            {     
                _settings.Acme.BaseUri = ChooseBaseUri();
            } 
            catch
            {
                _log.Error("Error choosing ACME server");
                return;
            }

            try
            {
                var configRoot = ChooseConfigPath();
                _settings.Client.ConfigurationPath = Path.Combine(configRoot, Settings.Acme.BaseUri.CleanUri());
                _settings.Client.LogPath = ChooseLogPath();
                _settings.Cache.Path = ChooseCachePath();

                EnsureFolderExists(configRoot, "configuration", true);
                EnsureFolderExists(_settings.Client.ConfigurationPath, "configuration", false);
                EnsureFolderExists(_settings.Client.LogPath, "log", !_settings.Client.LogPath.StartsWith(Settings.Client.ConfigurationPath));
                EnsureFolderExists(_settings.Cache.Path, "cache", !_settings.Client.LogPath.StartsWith(Settings.Client.ConfigurationPath));

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

        /// <summary>
        /// Choose the base URI based on command line options and/or global settings defaults
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public Uri ChooseBaseUri()
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
        /// Determine which folder to use for configuration data
        /// </summary>
        private string ChooseConfigPath()
        {
            var userRoot = Settings.Client.ConfigurationPath;
            string? configRoot;
            if (!string.IsNullOrEmpty(userRoot))
            {
                configRoot = userRoot;

                // Path configured in settings always wins, but
                // check for possible sub directories with client name
                // to keep bug-compatible with older releases that
                // created a subfolder inside of the users chosen config path
                var configRootWithClient = Path.Combine(userRoot, Settings.Client.ClientName);
                if (Directory.Exists(configRootWithClient))
                {
                    configRoot = configRootWithClient;
                }
            }
            else if (OperatingSystem.IsWindows() || Environment.IsPrivilegedProcess)
            {
                var appData = Environment.GetFolderPath(SpecialFolder.CommonApplicationData, SpecialFolderOption.DoNotVerify);
                configRoot = Path.Combine(appData, Settings.Client.ClientName);
            }
            else
            {
                // For non-elevated Linux we have to fall back to the user directory
                // These user will not be able to auto-renew.
                var appData = Environment.GetFolderPath(SpecialFolder.LocalApplicationData, SpecialFolderOption.DoNotVerify);
                configRoot = Path.Combine(appData, Settings.Client.ClientName);
            }
            return configRoot;
        }

        /// <summary>
        /// Determine which folder to use for logging
        /// </summary>
        private string ChooseLogPath()
        {
            if (string.IsNullOrWhiteSpace(Settings.Client.LogPath))
            {
                return Path.Combine(Settings.Client.ConfigurationPath, "Log");
            }
            else
            {
                // Create separate logs for each endpoint
                return Path.Combine(Settings.Client.LogPath, Settings.Acme.BaseUri.CleanUri());
            }
        }

        /// <summary>
        /// Determine which folder to use for cache certificates
        /// </summary>
        private string ChooseCachePath()
        {
            if (string.IsNullOrWhiteSpace(Settings.Cache.Path))
            {
                return Path.Combine(Settings.Client.ConfigurationPath, "Certificates");
            }
            return Settings.Cache.Path;
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