using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Services;
using System;
using System.IO;

namespace PKISharp.WACS.Configuration.Settings
{
    public class SettingsService
    {
        private const string _fileName = "settings.json";
        private readonly ILogService _log;
        private readonly FolderHelpers _folderHelpers;
        private readonly MainArguments? _arguments;
        private readonly InheritSettings _globalSettings = new();
        private readonly InheritSettings _settings = new();
        public ISettings Current => _settings;

        public SettingsService(ILogService log, ArgumentsParser parser)
        {
            _log = log;
            _folderHelpers = new FolderHelpers(log);

            if (!parser.ValidateMain())
            {
                return;
            }

            _arguments = parser.GetArguments<MainArguments>();
            if (_arguments == null)
            {
                return;
            }

            var globalSettings = LoadGlobalSettings();
            if (globalSettings == null)
            {
                return;
            }
            _globalSettings = globalSettings;
            _settings = globalSettings;

            Uri? defaultBaseUri;
            try
            {
                defaultBaseUri = ChooseBaseUri(_globalSettings);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error choosing ACME server");
                return;
            }

            try
            {
                _settings = ForBaseUri(defaultBaseUri);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error loading server settings");
                return;
            }

            // Configure disk logger
            _log.ApplyClientSettings(_settings.Client);
            _settings.Valid = true;
        }

        /// <summary>
        /// Get global settings from disk, and if they don't exist, try to create them from the template. If creation fails, return 
        /// the template as a fallback to at least have some settings available. If loading fails, return false to indicate that 
        /// settings are not available and the program should probably exit.
        /// </summary>
        /// <returns></returns>
        private InheritSettings? LoadGlobalSettings()
        {
            var globalFile = EnsureGlobalSettingsFile();
            if (globalFile == null)
            {
                return null;
            }
            _log.Verbose("Loading {settingsFileName} from {path}", globalFile.Name, globalFile.Directory?.FullName);
            InheritSettings? ret;
            try
            {
                ret = new InheritSettings(Settings.Load(globalFile));
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Unable to load global settings");
                while (ex.InnerException != null)
                {
                    _log.Error(ex.InnerException.Message);
                    ex = ex.InnerException;
                }
                return null;
            }
            try
            {
                _folderHelpers.EnsureFolderExists(ret.Client.ConfigRoot, "global configuration", true);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error initializing program");
                return null;
            }
            return ret;
        }

        /// <summary>
        /// Switch the settings to a specific base URI, by
        /// loading the server configuration from disk and merging 
        /// it with global settings. For now this is private but
        /// in the future it could be exposed to allow switching between multiple
        /// endpoints without restarting the program.
        /// </summary>
        /// <param name="baseUri"></param>
        /// <returns></returns>
        private InheritSettings ForBaseUri(Uri baseUri)
        {
            _log.Verbose("Loading settings for {baseUrI}", baseUri);
            _globalSettings.BaseUri = baseUri;
            var settings = LoadServerSettings(_globalSettings);
            if (settings != null)
            {
                _folderHelpers.EnsureFolderExists(settings.Client.ConfigRoot, "global configuration", true);
            }
            else
            {
                settings = _globalSettings;
            }
            var serverConfigPath = settings.Client.ConfigurationPath;
            var pathCompareMode =
                OperatingSystem.IsWindows() ?
                StringComparison.OrdinalIgnoreCase :
                StringComparison.Ordinal;
            _folderHelpers.EnsureFolderExists(serverConfigPath, "server configuration", true);
            _folderHelpers.EnsureFolderExists(settings.Client.LogPath, "log", !settings.Client.LogPath.StartsWith(serverConfigPath, pathCompareMode));
            _folderHelpers.EnsureFolderExists(settings.Cache.CachePath, "cache", !settings.Cache.CachePath.StartsWith(serverConfigPath, pathCompareMode));
            return settings;
        }

        /// <summary>
        /// Load server configuration from disk and marge with global settings.
        /// if no server configuration is found, global settings will be used as-is.
        /// If server configuration is found but fails to load, global settings will 
        /// be used as fallback.
        /// </summary>
        /// <param name="global"></param>
        /// <returns></returns>
        private InheritSettings? LoadServerSettings(InheritSettings global)
        {
            // Load overrides for settings at the server level
            var settings = new FileInfo(Path.Combine(global.Client.ConfigurationPath, _fileName));
            if (settings.Exists)
            {
                _log.Verbose("Loading {settingsFileName} from {path}", _fileName, global.Client.ConfigurationPath);
                try
                {
                    return global.MergeTyped(Settings.Load(settings), global.BaseUri);                
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Unable to load server settings from {settingsFileName}", _fileName);
                }
            }
            else
            {
                _log.Verbose("No {settingsFileName} found at {path}", _fileName, global.Client.ConfigurationPath);
            }
            return null;
        }

        /// <summary>
        /// Template file name depending on whether we 
        /// are running as a dotnet tool or not, and on the operating system.
        /// </summary>
        private static string TemplateName
        {
            get
            {
                var templateName = "settings_default.json";
                if (VersionService.DotNetTool)
                {
                    templateName = OperatingSystem.IsWindows() ? "settings.json" : "settings.linux.json";
                }
                return templateName;
            }
        }

        /// <summary>
        /// Figure out which file to use for global settings, and if it doesn't 
        /// exist, try to create it from the template. If creation fails, return 
        /// the template as a fallback to at least have some settings available.
        /// </summary>
        /// <returns></returns>
        private FileInfo? EnsureGlobalSettingsFile()
        {
            var settings = new FileInfo(Path.Combine(VersionService.SettingsPath, _fileName));
            if (!settings.Exists)
            {
                var settingsTemplate = new FileInfo(Path.Combine(VersionService.ResourcePath, TemplateName));
                if (!settingsTemplate.Exists)
                {
                    _log.Warning("Unable to locate {settings} in {path}", TemplateName, VersionService.ResourcePath);
                    return null;
                }
                else
                {
                    _log.Verbose("Copying {settingsFileTemplateName} to {settingsFileName}", TemplateName, _fileName);
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
                        _log.Warning(ex, "Unable to create {settingsFileName}, falling back to defaults. Try to run with elevated permissions to fix this issue.", _fileName);
                        return settingsTemplate;
                    }
                }
            }
            return settings;
        }

        /// <summary>
        /// Choose the base URI based on command line options and/or global settings defaults
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private Uri ChooseBaseUri(InheritSettings settings)
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
                if (settings.Acme.DefaultBaseUriTest?.IsAbsoluteUri ?? false)
                {
                    return settings.Acme.DefaultBaseUriTest;
                } 
                else
                {
                    _log.Warning("Setting Acme.DefaultBaseUriTest is unspecified or invalid, fallback to Acme.DefaultBaseUri");
                }
            }
            if (settings.Acme.DefaultBaseUri?.IsAbsoluteUri ?? false)
            {
                return settings.Acme.DefaultBaseUri;
            }
            else
            {
                _log.Error("Setting Acme.DefaultBaseUri is unspecified or invalid, please specify a valid absolute URI");
                throw new Exception();
            }
        }

    }
}