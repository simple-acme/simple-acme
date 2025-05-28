using PKISharp.WACS.Extensions;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.IO;
using static System.Environment;

namespace PKISharp.WACS.Configuration.Settings
{
    public interface IClientSettings
    {
        string ConfigRoot { get; }
        string ClientName { get; }
        string ConfigurationPath { get; }
        string LogPath { get; }
        bool? VersionCheck { get; }
    }

    internal class InheritClientSettings(ISettings root, params IEnumerable<ClientSettings?> chain) : InheritSettings<ClientSettings>(chain), IClientSettings
    {
        /// <summary>
        /// Determine which folder to use for configuration data
        /// </summary>
        public string ConfigRoot
        {
            get
            {
                var userRoot = Get(x => x.ConfigurationPath);
                if (!string.IsNullOrEmpty(userRoot))
                {
                    var configRoot = userRoot;
                    // CachePath configured in settings always wins, but
                    // check for possible sub directories with client name
                    // to keep bug-compatible with older releases that
                    // created a subfolder inside of the users chosen config path
                    var configRootWithClient = Path.Combine(userRoot, ClientName);
                    if (Directory.Exists(configRootWithClient))
                    {
                        configRoot = configRootWithClient;
                    }
                    return configRoot;
                }
                else if (OperatingSystem.IsWindows() || Environment.IsPrivilegedProcess)
                {
                    var appData = Environment.GetFolderPath(SpecialFolder.CommonApplicationData, SpecialFolderOption.DoNotVerify);
                    return Path.Combine(appData, ClientName);
                }
                else
                {
                    // For non-elevated Linux we have to fall back to the user directory
                    // These user will not be able to auto-renew.
                    var appData = Environment.GetFolderPath(SpecialFolder.LocalApplicationData, SpecialFolderOption.DoNotVerify);
                    return Path.Combine(appData, ClientName);
                }
            }
        }

        public string ClientName => Get(x => x.ClientName) ?? "simple-acme";
        public string ConfigurationPath => Path.Combine(ConfigRoot, root.BaseUri.CleanUri());
        public string LogPath {
            get
            {
                var userPath = Get(x => x.LogPath);
                if (string.IsNullOrWhiteSpace(userPath))
                {
                    return Path.Combine(ConfigurationPath, "Log");
                }
                else
                {
                    // Create separate logs for each endpoint
                    return Path.Combine(userPath, root.BaseUri.CleanUri());
                }
            }
        }
        public bool? VersionCheck => Get(x => x.VersionCheck) ?? false;
    }

    internal class ClientSettings
    {
        public string? ClientName { get; set; }
        public string? ConfigurationPath { get; set; }
        public string? LogPath { get; set; }
        public bool? VersionCheck { get; set; }
    }
}