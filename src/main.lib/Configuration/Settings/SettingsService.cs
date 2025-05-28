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
        private InheritSettings _settings = new();
        public ISettings Current => _settings;

        public SettingsService(ILogService log, ArgumentsParser parser)
        {
            _log = log;
            _folderHelpers = new FolderHelpers(log);

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
            if (!LoadGlobalSettings())
            {
                return;
            }
            try
            {
                _folderHelpers.EnsureFolderExists(_settings.Client.ConfigRoot, "configuration", true);
                _folderHelpers.EnsureFolderExists(_settings.Client.ConfigurationPath, "configuration", false);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error initializing program");
                return;
            }
            var serverSettings = LoadServerSettings();
            try
            {
                if (serverSettings)
                {
                    _folderHelpers.EnsureFolderExists(_settings.Client.ConfigRoot, "configuration", true);
                    _folderHelpers.EnsureFolderExists(_settings.Client.ConfigurationPath, "configuration", false);
                }
                var pathCompareMode =
                    OperatingSystem.IsWindows() ?
                    StringComparison.OrdinalIgnoreCase :
                    StringComparison.Ordinal;
                _folderHelpers.EnsureFolderExists(_settings.Client.LogPath, "log", !_settings.Client.LogPath.StartsWith(_settings.Client.ConfigurationPath, pathCompareMode));
                _folderHelpers.EnsureFolderExists(_settings.Cache.CachePath, "cache", !_settings.Client.LogPath.StartsWith(_settings.Client.ConfigurationPath, pathCompareMode));

                // Configure disk logger
                _log.ApplyClientSettings(Current.Client);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error initializing program");
                return;
            }
            _settings.Valid = true;
        }

        private bool LoadGlobalSettings()
        {
            var globalFile = EnsureGlobalSettingsFile();
            try
            {
                _settings = new InheritSettings(Settings.Load(globalFile));
            }
            catch (Exception ex)
            {
                _log.Error($"Unable to load {globalFile.Name}");
                while (ex.InnerException != null)
                {
                    _log.Error(ex.InnerException.Message);
                    ex = ex.InnerException;
                }
                return false;
            }
            try
            {
                _settings.BaseUri = ChooseBaseUri();
            }
            catch
            {
                _log.Error("Error choosing ACME server");
                return false;
            }
            return true;
        }

        private bool LoadServerSettings()
        {
            // Load overrides for settings at the server level
            var settingsFileName = _fileName;
            _log.Verbose("Looking for {settingsFileName} in {path}", settingsFileName, _settings.Client.ConfigurationPath);
            var settings = new FileInfo(Path.Combine(_settings.Client.ConfigurationPath, settingsFileName));
            if (settings.Exists)
            {
                try
                {
                    _settings = new InheritSettings([Settings.Load(settings), .. _settings.Settings]);
                    return true;
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Unable to load server settings from {settingsFileName}", settingsFileName);
                }
            }
            return false;
        }

        private FileInfo EnsureGlobalSettingsFile()
        {
            var settingsFileTemplateName = "settings_default.json";
            _log.Verbose("Looking for {settingsFileName} in {path}", _fileName, VersionService.SettingsPath);
            var settings = new FileInfo(Path.Combine(VersionService.SettingsPath, _fileName));
            var settingsTemplate = new FileInfo(Path.Combine(VersionService.ResourcePath, settingsFileTemplateName));
            var useFile = settings;
            if (!settings.Exists)
            {
                if (!settingsTemplate.Exists)
                {
                    // For .NET tool case
                    settingsTemplate = new FileInfo(Path.Combine(VersionService.ResourcePath, _fileName));
                }
                if (!settingsTemplate.Exists)
                {
                    _log.Warning("Unable to locate {settings}", _fileName);
                }
                else
                {
                    _log.Verbose("Copying {settingsFileTemplateName} to {settingsFileName}", settingsFileTemplateName, _fileName);
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
                        _log.Error(ex, "Unable to create {settingsFileName}, falling back to defaults", _fileName);
                        useFile = settingsTemplate;
                    }
                }
            }
            return useFile;
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
                if (_settings.Acme.DefaultBaseUriTest?.IsAbsoluteUri ?? false)
                {
                    return _settings.Acme.DefaultBaseUriTest;
                } 
                else
                {
                    _log.Warning("Setting Acme.DefaultBaseUriTest is unspecified or invalid, fallback to Acme.DefaultBaseUri");
                }
            }
            if (_settings.Acme.DefaultBaseUri?.IsAbsoluteUri ?? false)
            {
                return _settings.Acme.DefaultBaseUri;
            }
            else
            {
                _log.Error("Setting Acme.DefaultBaseUri is unspecified or invalid, please specify a valid absolute URI");
                throw new Exception();
            }
        }

    }
}