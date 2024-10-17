using PKISharp.WACS.Clients;
using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace PKISharp.WACS.Host
{
    internal class Banner(
        IIISClient iis,
        UpdateClient updateClient,
        ILogService logService,
        IInputService inputService,
        ISettingsService settingsService,
        IProxyService proxyService,
        ArgumentsParser argumentsParser,
        AdminService adminService,
        NetworkCheckService networkCheck,
        WacsJson wacsJson,
        IAutoRenewService taskSchedulerService)
    {
        /// <summary>
        /// Show banner
        /// </summary>
        public async Task ShowBanner()
        {
            // Version information
            logService.Dirty = true;
            inputService.CreateSpace();
            logService.Information(LogType.Screen, "A simple cross platform ACME client (WACS)");
            logService.Information(LogType.Screen, "Software version {version} ({build}, {bitness})", VersionService.SoftwareVersion, VersionService.BuildType, VersionService.Bitness);
            logService.Information(LogType.Disk | LogType.Event, "Software version {version} ({build}, {bitness}) started", VersionService.SoftwareVersion, VersionService.BuildType, VersionService.Bitness);
            logService.Debug("Running on {platform} {version}", Environment.OSVersion.Platform, Environment.OSVersion.Version);
            argumentsParser.ShowCommandLine();

            // Connection test
            logService.Information("Connecting to {ACME}...", settingsService.BaseUri);
            var result = networkCheck.CheckNetwork();
            try
            {
                await result.WaitAsync(TimeSpan.FromSeconds(30));
            }
            catch (TimeoutException)
            {
                try
                {
                    logService.Warning("Network check failed or timed out, retry with proxy bypass...");
                    proxyService.Disable();
                    result = networkCheck.CheckNetwork();
                    await result.WaitAsync(TimeSpan.FromSeconds(30));
                }
                catch (TimeoutException)
                {
                    logService.Warning("Network check failed or timed out. Functionality may be limited.");
                }
            }

            // New version test
            if (settingsService.Client.VersionCheck)
            {
                inputService.CreateSpace();
                await updateClient.CheckNewVersion();
            }

            // IIS version test
            if (OperatingSystem.IsWindows())
            {
                if (adminService.IsAdmin)
                {
                    logService.Debug("Running as administrator");
                    if (iis.Version.Major > 0)
                    {
                        logService.Debug("IIS version {version}", iis.Version);
                    }
                    else
                    {
                        logService.Debug("IIS not detected");
                    }
                }
                else
                {
                    logService.Warning("Running as limited user, some options disabled");
                }
            }
            else
            {
                if (adminService.IsAdmin)
                {
                    logService.Debug("Running as superuser/root");
                }
                else
                {
                    logService.Warning("Running as limited user, some options *including autorenewal* disabled");
                }
            }

            // Task scheduler health check
            taskSchedulerService.ConfirmAutoRenew();

            // Further information and tests
            logService.Information(LogType.Screen, "Check the manual at {webiste}", "https://simple-acme.com");
            logService.Information(LogType.Screen, "Please leave a {star} at {url}", "★", "https://github.com/simple-acme/simple-acme");
            logService.Verbose("Unicode test: Mandarin/{chinese} Cyrillic/{russian} Arabic/{arab}", "語言", "язык", "لغة");
        }

        /// <summary>
        /// Output program information as JSON for automation
        /// </summary>
        /// <returns></returns>
        public void WriteJson()
        {
            // Version information
            var data = new BannerData
            {
                SoftwareVersion = VersionService.SoftwareVersion,
                Debug = VersionService.Debug,
                DotNetTool = VersionService.DotNetTool,
                Pluggable = VersionService.Pluggable,
                Bitness = VersionService.Bitness,
                BaseUri = settingsService.BaseUri,
                ConfigurationPath = settingsService.Client.ConfigurationPath,
                LogPath = settingsService.Client.LogPath,
                CachePath = settingsService.Cache.Path,
                SettingsPath = VersionService.SettingsPath,
                PluginPath = VersionService.PluginPath,
                ExecutablePath = VersionService.ExePath
            };
            Console.WriteLine(JsonSerializer.Serialize(data, wacsJson.BannerData));
        }
    }

    public class BannerData()
    {
        public Version? SoftwareVersion { get; init; }
        public string? Bitness { get; internal set; }
        public Uri? BaseUri { get; internal set; }
        public string? ConfigurationPath { get; internal set; }
        public string? LogPath { get; internal set; }
        public string? CachePath { get; internal set; }
        public string? SettingsPath { get; internal set; }
        public string? PluginPath { get; internal set; }
        public string? ExecutablePath { get; internal set; }
        public bool Debug { get; internal set; }
        public bool DotNetTool { get; internal set; }
        public bool Pluggable { get; internal set; }
    }
}