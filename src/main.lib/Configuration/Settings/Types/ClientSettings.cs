using PKISharp.WACS.Extensions;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.IO;
using static System.Environment;

namespace PKISharp.WACS.Configuration.Settings.Types
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
                else if (OperatingSystem.IsWindows() || IsPrivilegedProcess)
                {
                    var appData = GetFolderPath(SpecialFolder.CommonApplicationData, SpecialFolderOption.DoNotVerify);
                    return Path.Combine(appData, ClientName);
                }
                else
                {
                    // For non-elevated Linux we have to fall back to the user directory
                    // These user will not be able to auto-renew.
                    var appData = GetFolderPath(SpecialFolder.LocalApplicationData, SpecialFolderOption.DoNotVerify);
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

    public class ClientSettings
    {
        [SettingsValue(
            Default = "simple-acme", 
            Description = "The name of the client, which comes back in several places like the " +
            "scheduled task name, Windows event viewer, notification messages, user agent and " +
            "the <code>ConfigurationPath</code>.")]
        public string? ClientName { get; set; }
        
        [SettingsValue(
            Default = "null",
            SubType = "path",
            NullBehaviour = "resolves to <code>%programdata%\\{Client.ClientName}\\{ACME.DefaultBaseUri}</code>",
            Description = "Change the location where the program stores its (temporary) files.")]
        public string? ConfigurationPath { get; set; }

        [SettingsValue(
            Default = "null",
            SubType = "path",
            NullBehaviour = "resolves to <code>{Client.ConfigurationPath}\\Log</code>",
            Description = "The path where log files for the past 31 days are stored.")]
        public string? LogPath { get; set; }

        [SettingsValue(
            Default = "false",
            Description = "Automatically check for new versions at startup.")]
        public bool? VersionCheck { get; set; }
    }
}