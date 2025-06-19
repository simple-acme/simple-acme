using Autofac;
using Autofac.Core;
using PKISharp.WACS.Clients;
using PKISharp.WACS.Clients.Acme;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Legacy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Host
{
    internal class MainMenu(
            ISharingLifetimeScope container,
            IAutofacBuilder scopeBuilder,
            ExceptionHandler exceptionHandler,
            ILogService log,
            ISettings settings,
            IUserRoleService userRoleService,
            IInputService input,
            DueDateStaticService dueDateService,
            IRenewalStore renewalStore,
            ArgumentsParser argumentsParser,
            AdminService adminService,
            RenewalCreator renewalCreator,
            RenewalManager renewalManager,
            IAutoRenewService taskScheduler,
            SecretServiceManager secretServiceManager,
            AccountManager accountManager,
            AcmeClientManager clientManager,
            ValidationOptionsService validationOptionsService)
    {

        private readonly MainArguments _args = argumentsParser.GetArguments<MainArguments>() ?? new MainArguments();
        
        /// <summary>
        /// Main user experience
        /// </summary>
        public async Task MainMenuEntry(RunLevel runLevel)
        {
            var renewals = await renewalStore.List();
            var total = renewals.Count;
            var due = renewals.Count(dueDateService.IsDue);
            var error = renewals.Count(x => !x.History.LastOrDefault()?.Success ?? false);
            var options = new List<Choice<Func<Task>>>
            {
                Choice.Create(
                    () => renewalCreator.SetupRenewal(runLevel | RunLevel.Simple), 
                    "Create certificate (default settings)", "N", 
                    @default: true),
                Choice.Create(
                    () => renewalCreator.SetupRenewal(runLevel | RunLevel.Advanced),
                    "Create certificate (full options)", "M"),
                Choice.Create(
                    () => renewalManager.CheckRenewals(runLevel),
                    $"Run renewals ({due} currently due)", "R",
                    color: due == 0 ? null : ConsoleColor.Yellow,
                    state: total == 0 ? State.DisabledState("No renewals have been created yet.") : State.EnabledState()),
                Choice.Create(
                    () => renewalManager.ManageRenewals(),
                    $"Manage renewals ({total} total{(error == 0 ? "" : $", {error} in error")})", "A",
                    color: error == 0 ? null : ConsoleColor.Red,
                    state: total == 0 ? State.DisabledState("No renewals have been created yet.") : State.EnabledState()),
                Choice.Create(
                    () => ExtraMenu(), 
                    "More options...", "O"),
                Choice.Create(
                    () => { _args.CloseOnFinish = true; _args.Test = false; return Task.CompletedTask; }, 
                    "Quit", "Q")
            };
            var chosen = await input.ChooseFromMenu("Please choose from the menu", options);
            await chosen.Invoke();
        }

        /// <summary>
        /// Less common options
        /// </summary>
        private async Task ExtraMenu()
        {
            var options = new List<Choice<Func<Task>>>
            {
                Choice.Create(
                    secretServiceManager.ManageSecrets,
                    $"Manage secrets", "S"),
                Choice.Create<Func<Task>>(
                    () => validationOptionsService.Manage(container),
                    $"Manage global validation options", "V"),
                Choice.Create<Func<Task>>(
                    () => taskScheduler.SetupAutoRenew(RunLevel.Interactive | RunLevel.Advanced | RunLevel.ForceTaskScheduler), 
                    OperatingSystem.IsWindows() ? "(Re)create scheduled task" : "(Re)create cronjob", "TData",
                    state: !userRoleService.AllowAutoRenew ? State.DisabledState(OperatingSystem.IsWindows() ? "Run as an administrator to allow access to the task scheduler." : "Run as a superuser to allow scheduling cronjob.") : State.EnabledState()),
                Choice.Create<Func<Task>>(
                    () => container.Resolve<NotificationService>().NotifyTest(), 
                    "Test notification", "E"),
                Choice.Create<Func<Task>>(
                    () => UpdateAccount(RunLevel.Interactive),
                    accountManager.ListAccounts().Any() ? "ACME account details" : "Create ACME account", "A"),
                Choice.Create<Func<Task>>(
                    () => Import(RunLevel.Interactive | RunLevel.Advanced), 
                    "Import scheduled renewals from WACS/LEWS 1.9.x", "I",
                    state: !adminService.IsAdmin ? State.DisabledState("Run as an administrator to allow search for legacy renewals.") : State.EnabledState()),
                Choice.Create<Func<Task>>(
                    () => Encrypt(RunLevel.Interactive), 
                    "Encrypt/decrypt configuration", "M"),
                Choice.Create<Func<Task>>(
                    () => container.Resolve<UpdateClient>().CheckNewVersion(),
                    "Check for updates", "U"),
                Choice.Create<Func<Task>>(
                    () => Task.CompletedTask, 
                    "Back", "Q",
                    @default: true)
            };
            var chosen = await input.ChooseFromMenu("Please choose from the menu", options);
            await chosen.Invoke();
        }

        /// <summary>
        /// Load renewals from 1.9.account
        /// </summary>
        internal async Task Import(RunLevel runLevel)
        {
            var importUri = !string.IsNullOrEmpty(_args.ImportBaseUri) ? 
                new Uri(_args.ImportBaseUri) : 
                settings.Acme.DefaultBaseUriImport;
            if (runLevel.HasFlag(RunLevel.Interactive))
            {
                var alt = await input.RequestString($"Importing renewals for {importUri}, enter to accept or type an alternative");
                if (!string.IsNullOrEmpty(alt))
                {
                    importUri = new Uri(alt);
                }
            }
            if (importUri != null)
            {
                using var scope = scopeBuilder.Legacy(container, importUri, settings.BaseUri);
                var importer = scope.Resolve<Importer>();
                await importer.Import(runLevel);
            }
        }

        /// <summary>
        /// Encrypt/Decrypt all machine-dependent information
        /// </summary>
        internal async Task Encrypt(RunLevel runLevel)
        {
            var userApproved = !runLevel.HasFlag(RunLevel.Interactive);
            var encryptConfig = settings.Security.EncryptConfig;
            if (!userApproved)
            {
                input.Show(null, "To move your installation of simple-acme to another machine, you will want " +
                "to copy the data directory's files to the new machine. However, if you use the Encrypted Configuration option, your renewal " +
                "files contain protected data that is dependent on your local machine. You can " +
                "use this tools to temporarily unprotect your data before moving from the old machine. " +
                "The renewal files includes passwords for your certificates, other passwords/keys, and a key used " +
                "for signing requests for new certificates.");
                input.CreateSpace();
                input.Show(null, "To remove machine-dependent protections, use the following steps.");
                input.Show(null, "  1. On your old machine, set the EncryptConfig setting to false");
                input.Show(null, "  2. Run this option; all protected values will be unprotected.");
                input.Show(null, "  3. Copy your data files to the new machine.");
                input.Show(null, "  4. On the new machine, set the EncryptConfig setting to true");
                input.Show(null, "  5. Run this option; all unprotected values will be saved with protection");
                input.CreateSpace();
                input.Show(null, $"Data directory: {settings.Client.ConfigurationPath}");
                input.Show(null, $"Config directory: {new FileInfo(VersionService.ExePath).Directory?.FullName}\\settings.json");
                input.Show(null, $"Current EncryptConfig setting: {encryptConfig}");
                userApproved = await input.PromptYesNo($"Save all renewal files {(encryptConfig ? "with" : "without")} encryption?", false);
            }
            if (userApproved)
            {
                await renewalStore.Encrypt(); //re-saves all renewals, forcing re-write of all protected strings 

                var accountManager = container.Resolve<AccountManager>();
                await accountManager.Encrypt(); //re-writes the signer file

                var cacheService = container.Resolve<ICacheService>();
                await cacheService.Encrypt(); //re-saves all cached private keys

                var secretService = container.Resolve<SecretServiceManager>();
                await secretService.Encrypt(); //re-writes the secrets file

                var orderManager = container.Resolve<OrderManager>();
                await orderManager.Encrypt(); //re-writes the cached order files

                var validationOptionsService = container.Resolve<IValidationOptionsService>();
                await validationOptionsService.Encrypt(); //re-saves all global validation options

                log.Information("Your files are re-saved with encryption turned {onoff}", encryptConfig ? "on" : "off");
            }
        }

        /// <summary>
        /// Check/update account information
        /// </summary>
        /// <param name="runLevel"></param>
        private async Task UpdateAccount(RunLevel runLevel)
        {
            var renewals = await renewalStore.List();
            var accounts = accountManager.ListAccounts();
            var account = accounts.FirstOrDefault();
            if (accounts.Count() > 1)
            {
                account = await input.ChooseRequired(
                    "Choose ACME account to view/update",
                    accounts,
                    account => {
                        var count = renewals.Where(r => (r.Account ?? "") == account).Count();
                        var label = $"({count} renewal{(count != 1 ? "s" : "")})";
                        return new Choice<string>(account)
                        {
                            Description = account == "" ? $"Default account {label}" : $"Named account: {account} {label}",
                            Default = string.Equals(account, "", StringComparison.OrdinalIgnoreCase),
                        };
                    });
            }
            var client = await clientManager.GetClient(account) ?? throw new InvalidOperationException("Unable to initialize acmeAccount");
            var accountDetails = client.Account.Details;
            input.CreateSpace();
            input.Show("Account ID", accountDetails.Payload.Id ?? "-");
            input.Show("Account KID", accountDetails.Kid ?? "-");
            input.Show("Created", accountDetails.Payload.CreatedAt);
            input.Show("Initial IP", accountDetails.Payload.InitialIp);
            input.Show("Status", accountDetails.Payload.Status);
            if (accountDetails.Payload.Contact != null &&
                accountDetails.Payload.Contact.Length > 0)
            {
                input.Show("Contact(s)", string.Join(", ", accountDetails.Payload.Contact));
            }
            else
            {
                input.Show("Contact(s)", "(none)");
            }
            if (await input.PromptYesNo("Modify contacts?", false))
            {
                try
                {
                    await clientManager.ChangeContacts(account);
                    await UpdateAccount(runLevel);
                } 
                catch (Exception ex)
                {
                    exceptionHandler.HandleException(ex);
                }
            }
        }

        /// <summary>
        /// Add a new global validation option from the command line
        /// </summary>
        /// <returns></returns>
        internal async Task AddGlobalValidationOption() => await validationOptionsService.Add(container, _args.GlobalValidationPattern, _args.GlobalValidationPriority);
    }
}