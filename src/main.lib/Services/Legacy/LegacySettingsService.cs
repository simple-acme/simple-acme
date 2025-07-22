﻿using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Configuration.Settings;
using PKISharp.WACS.Configuration.Settings.Types;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace PKISharp.WACS.Host.Services.Legacy
{
    public class LegacySettingsService : ISettings
    {
        private readonly ILogService _log;
        private readonly ClientSettings _client = new();
        public IClientSettings Client => new InheritClientSettings(this, _client);
        public IUiSettings UI { get; private set; } = new InheritUiSettings(new UiSettings());
        public IAcmeSettings Acme { get; private set; } = new InheritAcmeSettings(new AcmeSettings());
        public IExecutionSettings Execution { get; private set; } = new InheritExecutionSettings(new ExecutionSettings());
        public IProxySettings Proxy { get; private set; } = new InheritProxySettings(new ProxySettings());
        public ICacheSettings Cache => new InheritCacheSettings(this, new CacheSettings());
        public IScheduledTaskSettings ScheduledTask { get; private set; } = new InheritScheduledTaskSettings(new ScheduledTaskSettings());
        public INotificationSettings Notification { get; private set; } = new InheritNotificationSettings(new NotificationSettings());
        public ISecuritySettings Security { get; private set; } = new InheritSecuritySettings(new SecuritySettings());
        public IScriptSettings Script { get; private set; } = new InheritScriptSettings(new ScriptSettings());
        public ISourceSettings Source { get; private set; } = new InheritSourceSettings(new SourceSettings());
        public IValidationSettings Validation { get; private set; } = new InheritValidationSettings(new ValidationSettings());
        public IOrderSettings Order { get; private set; } = new InheritOrderSettings(new OrderSettings());
        public ICsrSettings Csr { get; private set; } = new InheritCsrSettings(new CsrSettings());
        public IStoreSettings Store { get; private set; } = new InheritStoreSettings(new StoreSettings());
        public IInstallationSettings Installation { get; private set; } = new InheritInstallationSettings(new InstallationSettings());
        public ISecretsSettings Secrets { get; private set; } = new InheritSecretsSettings(new SecretsSettings()); 
        public List<string> ClientNames { get; private set; }
        public Uri BaseUri { get; private set; }
        public bool Valid => true;

        public LegacySettingsService(ILogService log, MainArguments main, ISettings settings)
        {
            _log = log;
            UI = settings.UI;
            Acme = settings.Acme;
            ScheduledTask = settings.ScheduledTask;
            Notification = settings.Notification;
            Security = settings.Security;
            Script = settings.Script;
            // Copy so that the "ConfigurationPath" setting is not modified
            // from outside anymore
            _client = new ClientSettings()
            {
                ClientName = settings.Client.ClientName,
                ConfigurationPath = settings.Client.ConfigurationPath,
                LogPath = settings.Client.LogPath
            };
            Validation = settings.Validation;
            Store = settings.Store;

            ClientNames = [ 
                settings.Client.ClientName,
                "simple-acme",
                "win-acme", 
                "letsencrypt-win-simple"
            ];

            // Read legacy configuration file
            var installDir = new FileInfo(VersionService.ExePath).DirectoryName;
            var legacyConfig = new FileInfo(Path.Combine(installDir!, "settings.config"));
            var userRoot = default(string);
            if (legacyConfig.Exists)
            {
                var configXml = new XmlDocument();
                configXml.LoadXml(File.ReadAllText(legacyConfig.FullName));

                // Get custom configuration path:
                var configPath = configXml.SelectSingleNode("//setting[@name='ConfigurationPath']/value")?.InnerText ?? "";
                if (!string.IsNullOrEmpty(configPath))
                {
                    userRoot = configPath;
                }

                // Get custom client name: 
                var customName = configXml.SelectSingleNode("//setting[@name='ClientName']/value")?.InnerText ?? "";
                if (!string.IsNullOrEmpty(customName))
                {
                    ClientNames.Insert(0, customName);
                }
            }
            BaseUri = new Uri(main.BaseUri);
            CreateConfigPath(main, userRoot);
        }

        private void CreateConfigPath(MainArguments options, string? userRoot)
        {
            var configRoot = "";
            if (!string.IsNullOrEmpty(userRoot))
            {
                configRoot = userRoot;

                // Path configured in settings always wins, but
                // check for possible sub directories with client name
                // to keep bug-compatible with older releases that
                // created a subfolder inside of the users chosen config path
                foreach (var clientName in ClientNames)
                {
                    var configRootWithClient = Path.Combine(userRoot, clientName);
                    if (Directory.Exists(configRootWithClient))
                    {
                        configRoot = configRootWithClient;
                        _client.ClientName = clientName;
                        break;
                    }
                }
            }
            else
            {
                // When using a system folder, we have to create a sub folder
                // with the most preferred client name, but we should check first
                // if there is an older folder with an less preferred (older)
                // client name.
                var roots = new List<string>
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
                };
                foreach (var root in roots)
                {
                    // Stop looking if the directory has been found
                    if (!Directory.Exists(configRoot))
                    {
                        foreach (var clientName in ClientNames.ToArray().Reverse())
                        {
                            _client.ClientName = clientName;
                            configRoot = Path.Combine(root, clientName);
                            if (Directory.Exists(configRoot))
                            {
                                // Stop looking if the directory has been found
                                break;
                            }
                        }
                    }
                }
            }

            _client.ConfigurationPath = Path.Combine(configRoot, CleanFileName(options.BaseUri));
            _log.Debug("Legacy config folder: {_configPath}", Client.ConfigurationPath);
        }

        public static string CleanFileName(string fileName) => Path.GetInvalidFileNameChars().Aggregate(fileName, (current, c) => current.Replace(c.ToString(), string.Empty));
        public ISettings Merge(Settings settings) => this;
    }
}