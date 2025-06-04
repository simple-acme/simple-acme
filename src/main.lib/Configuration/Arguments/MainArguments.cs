﻿using PKISharp.WACS.Plugins.TargetPlugins;

namespace PKISharp.WACS.Configuration.Arguments
{
    public class MainArguments : BaseArguments
    {
        public override string Name => "Main";
        public override bool Active(string[] args)
        {
            return
                !string.IsNullOrEmpty(Installation) ||
                !string.IsNullOrEmpty(Store) ||
                !string.IsNullOrEmpty(Order) ||
                !string.IsNullOrEmpty(Csr) ||
                !string.IsNullOrEmpty(Target) ||
                !string.IsNullOrEmpty(Source) ||
                !string.IsNullOrEmpty(Validation);
        }

        public bool HasFilter =>
            !string.IsNullOrEmpty(Id) ||
            !string.IsNullOrEmpty(FriendlyName);

        // Basic options

        [CommandLine(Description = "Address of the ACME server to use. The default endpoint can be modified in settings.json.")]
        public string BaseUri { get; set; } = "";

        [CommandLine(Description = "Import scheduled renewals from version 1.9.x in unattended mode.", Obsolete = true)]
        public bool Import { get; set; }

        [CommandLine(Description = "[--import] When importing scheduled renewals from version 1.9.x, this argument can change the address of the ACMEv1 server to import from. The default endpoint to import from can be modified in settings.json.", Obsolete = true)]
        public string? ImportBaseUri { get; set; }

        [CommandLine(Description = "Enables testing behaviours in the program which may help with troubleshooting. By default this also switches the --baseuri to the ACME test endpoint. The default endpoint for test mode can be modified in settings.json.")]
        public bool Test { get; set; }

        [CommandLine(Description = "Print additional log messages to console for troubleshooting and bug reports.")]
        public bool Verbose { get; set; }

        [CommandLine(Description = "Show information about all available command line options.")]
        public bool Help { get; set; }

        [CommandLine(Description = "Generate YML describing command line arguments for the documentation website.", Obsolete = true)]
        public bool Docs { get; set; }

        [CommandLine(Description = "Show version information.")]
        public bool Version { get; set; }

        [CommandLine(Description = "Output configuration information in JSON format.")]
        public bool Config { get; set; }

        // Renewal

        [CommandLine(Description = "Renew any certificates that are due. This argument is used by the scheduled task. Note that it's not possible to change certificate properties and renew at the same time.")]
        public bool Renew { get; set; }

        [CommandLine(Description = "[--renew] Always execute the renewal, disregarding the validity of the current certificates and the prefered schedule.")]
        public bool Force { get; set; }

        [CommandLine(Description = "Bypass the cache on certificate requests. Applies to both new requests and renewals.")]
        public bool NoCache { get; set; }

        // Commands

        [CommandLine(Description = "Create an ACME service account without creating a certificate.")]
        public bool Register { get; set; }

        [CommandLine(Description = "Cancel renewal specified by the --friendlyname or --id arguments.")]
        public bool Cancel { get; set; }

        [CommandLine(Description = "Revoke the most recently issued certificate for the renewal specified by the --friendlyname or --id arguments.")]
        public bool Revoke { get; set; }

        [CommandLine(Description = "List all created renewals in unattended mode.")]
        public bool List { get; set; }

        [CommandLine(Description = "Rewrites all renewal information using current EncryptConfig setting")]
        public bool Encrypt { get; set; }

        // Targeting

        [CommandLine(Description = "[--source|--cancel|--renew|--revoke] Id of a new or existing renewal, can be used to override the default when creating a new renewal or to specify a specific renewal for other commands.")]
        public string? Id { get; set; }

        [CommandLine(Description = "[--source|--cancel|--renew|--revoke] Friendly name of a new or existing renewal, can be used to override the default when creating a new renewal or to specify a specific renewal for other commands. In the latter case a pattern might be used. " + IISArguments.PatternExamples)]
        public string? FriendlyName { get; set; }

        [CommandLine(Description = "Specify which target plugin to run, bypassing the main menu and triggering unattended mode.", Obsolete = true)]
        public string? Target { get; set; }

        [CommandLine(Description = "Specify which source plugin to run, bypassing the main menu and triggering unattended mode.")]
        public string? Source { get; set; }

        [CommandLine(Description = "Specify which validation plugin to run. If none is specified, SelfHosting validation will be chosen as the default.")]
        public string? Validation { get; set; }

        [CommandLine(Description = "Specify which validation mode to use. HTTP-01 is the default.")]
        public string? ValidationMode { get; set; }

        [CommandLine(Description = "Specify which order plugin to use. Single is the default.")]
        public string? Order { get; set; }

        [CommandLine(Description = "Specify which CSR plugin to use. RSA is the default.")]
        public string? Csr { get; set; }

        [CommandLine(Description = "Specify which store plugin to use. CertificateStore is the default. This may be a comma-separated list.")]
        public string? Store { get; set; }

        [CommandLine(Description = "Specify which installation plugins to use (if any). This may be a comma-separated list.")]
        public string? Installation { get; set; }

        [CommandLine(Description = "Specify which certificate profile to use.")]
        public string? Profile { get; set; }

        // Vault manipulation

        [CommandLine(Description = "Store a new value in the secret vault, or overwrite an existing one.")]
        public bool VaultStore { get; set; }

        [CommandLine(Description = "Key to target for vault commands. This should be in the format like vault://json/mysecret.")]
        public string? VaultKey { get; set; }

        [CommandLine(Description = "Secret to save in the vault.", Secret = true)]
        public string? VaultSecret { get; set; }

        // Misc

        [CommandLine(Description = "[--test] Close the application when complete, which usually does not happen when test mode is active. Useful to test unattended operation.")]
        public bool CloseOnFinish { get; set; }

        [CommandLine(Description = "Hide sites that have existing https bindings from interactive mode.")]
        public bool HideHttps { get; set; }

        [CommandLine(Description = "Do not create (or offer to update) the scheduled task.")]
        public bool NoTaskScheduler { get; set; }

        [CommandLine(Obsolete = true, Description = "Avoid the question about specifying the task scheduler user, as such defaulting to the SYSTEM account.")]
        public bool UseDefaultTaskUser { get; set; }

        [CommandLine(Description = "Create or update the scheduled task according to the current settings.")]
        public bool SetupTaskScheduler { get; set; }
    }
}