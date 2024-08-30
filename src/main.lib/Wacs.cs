using PKISharp.WACS.Clients;
using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Services;
using System;
using System.Threading.Tasks;

namespace PKISharp.WACS.Host
{
    internal class Wacs(
        ExceptionHandler exceptionHandler,
        IIISClient iis,
        UpdateClient updateClient,
        ILogService logService,
        IInputService inputService,
        ISettingsService settingsService,
        HelpService helpService,
        VersionService versionService,
        ArgumentsParser argumentsParser,
        AdminService adminService,
        RenewalCreator renewalCreator,
        NetworkCheckService networkCheck,
        RenewalManager renewalManager,
        Unattended unattended,
        IAutoRenewService taskSchedulerService,
        MainMenu mainMenu)
    {
        private MainArguments _args = new();

        public void SetEncoding()
        {
            if (!string.IsNullOrWhiteSpace(settingsService.UI.TextEncoding))
            {
                try
                {
                    var encoding = System.Text.Encoding.GetEncoding(settingsService.UI.TextEncoding);
                    Console.OutputEncoding = encoding;
                    Console.InputEncoding = encoding;
                    Console.Title = $"simple-acme {VersionService.SoftwareVersion}";
                }
                catch (Exception ex)
                {
                    logService.Warning(ex, "Error setting text encoding to {name}", settingsService.UI.TextEncoding);
                }
            }
        }

        /// <summary>
        /// Main program
        /// </summary>
        public async Task<int> Start()
        {
            // Exit when settings are not valid. The settings service
            // also checks the command line arguments
            if (!settingsService.Valid)
            {
                return -1;
            }
            if (!versionService.Init())
            {
                return -1;
            }

            // List informational message and start-up diagnostics
            _args = argumentsParser.GetArguments<MainArguments>() ?? new();

            // Set console window encoding
            SetEncoding();

            await ShowBanner();

            // Version display
            if (_args.Version)
            {
                await CloseDefault();
                if (_args.CloseOnFinish)
                {
                    return 0;
                }
            }

            // Help function
            if (_args.Help)
            {
                helpService.ShowArguments();
#if DEBUG
                helpService.ShowArgumentsYaml();
#endif
                await CloseDefault();
                if (_args.CloseOnFinish)
                {
                    return 0;
                }
            }

            // Base runlevel flags on command line arguments
            var unattendedRunLevel = RunLevel.Unattended;
            var interactiveRunLevel = RunLevel.Interactive;
            if (_args.Force)
            {
                unattendedRunLevel |= RunLevel.Force | RunLevel.NoCache;
            }
            if (_args.NoCache)
            {
                interactiveRunLevel |= RunLevel.Test;
                unattendedRunLevel |= RunLevel.NoCache;
            }
            if (_args.Test)
            {
                interactiveRunLevel |= RunLevel.Test;
                unattendedRunLevel |= RunLevel.Test;
            }

            // Main loop
            do
            {
                try
                {
                    if (_args.Import)
                    {
                        await mainMenu.Import(unattendedRunLevel);
                        await CloseDefault();
                    }
                    else if (_args.List)
                    {
                        await unattended.List();
                        await CloseDefault();
                    }
                    else if (_args.Cancel)
                    {
                        await unattended.Cancel();
                        await CloseDefault();
                    }
                    else if (_args.Revoke)
                    {
                        await unattended.Revoke();
                        await CloseDefault();
                    }
                    else if (_args.Register)
                    {
                        await unattended.Register();
                        await CloseDefault();
                    }
                    else if (_args.Renew)
                    {
                        await renewalManager.CheckRenewals(unattendedRunLevel);
                        await CloseDefault();
                    }
                    else if (!string.IsNullOrEmpty(_args.Target) || !string.IsNullOrEmpty(_args.Source))
                    {
                        await renewalCreator.SetupRenewal(unattendedRunLevel);
                        await CloseDefault();
                    }
                    else if (_args.Encrypt)
                    {
                        await mainMenu.Encrypt(unattendedRunLevel);
                        await CloseDefault();
                    }
                    else if (_args.SetupTaskScheduler)
                    {
                        await taskSchedulerService.SetupAutoRenew(unattendedRunLevel);
                        await CloseDefault();
                    }
                    else
                    {
                        await mainMenu.MainMenuEntry(interactiveRunLevel);
                    }
                }
                catch (Exception ex)
                {
                    exceptionHandler.HandleException(ex);
                    await CloseDefault();
                }
                if (!_args.CloseOnFinish)
                {
                    _args.Clear();
                    exceptionHandler.ClearError();
                    iis.Refresh();
                }
            }
            while (!_args.CloseOnFinish);

            // Return control to the caller
            logService.Verbose("Exiting with status code {code}", exceptionHandler.ExitCode);
            return exceptionHandler.ExitCode;
        }

        /// <summary>
        /// Show banner
        /// </summary>
        private async Task ShowBanner()
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
            await result.WaitAsync(TimeSpan.FromSeconds(30));
            if (!result.IsCompletedSuccessfully)
            {
                logService.Warning("Network check failed or timed out, retry without proxy detection...");
                settingsService.Proxy.Url = null;
                result = networkCheck.CheckNetwork();
                await result.WaitAsync(TimeSpan.FromSeconds(30));
            }
            if (!result.IsCompletedSuccessfully)
            {
                logService.Warning("Network check failed or timed out. Functionality may be limited.");
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
        /// Present user with the option to close the program
        /// Useful to keep the console output visible when testing
        /// unattended commands
        /// </summary>
        private async Task CloseDefault()
        {
            _args.CloseOnFinish =
                !_args.Test ||
                _args.CloseOnFinish || 
                await inputService.PromptYesNo("[--test] Quit?", true);
        }
    }
}