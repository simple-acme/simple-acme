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
        Banner banner,
        ILogService logService,
        IInputService inputService,
        ISettingsService settingsService,
        HelpService helpService,
        VersionService versionService,
        ArgumentsParser argumentsParser,
        RenewalCreator renewalCreator,
        DomainParseService domainParseService,
        SecretServiceManager secretServiceManager,
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

            // JSON banner for automation
            if (_args.Config)
            {
                banner.WriteJson();
                return 0;
            }

            // Text banner for regular use
            await banner.ShowBanner();

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
                await CloseDefault();
                if (_args.CloseOnFinish)
                {
                    return 0;
                }
            }

            // Documentation website helper, hidden from user
            // but used by the CI/CD system to automatically 
            // update the website.
            if (_args.Docs)
            {
                helpService.GenerateArgumentsYaml();
                helpService.GeneratePluginsYaml();
                return 0;
            }

            // Initialize domain parser
            await domainParseService.Initialize();

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
                if (_args.NoCache)
                {
                    interactiveRunLevel |= RunLevel.ForceValidation;
                    unattendedRunLevel |= RunLevel.ForceValidation;
                }
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
                        await taskSchedulerService.SetupAutoRenew(unattendedRunLevel | RunLevel.ForceTaskScheduler);
                        await CloseDefault();
                    }
                    else if (_args.VaultStore)
                    {
                        await secretServiceManager.StoreSecret(_args.VaultKey, _args.VaultSecret);
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