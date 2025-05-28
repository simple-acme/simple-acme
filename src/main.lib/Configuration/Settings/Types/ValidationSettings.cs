using PKISharp.WACS.Configuration.Settings.Types.Validation;
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Configuration.Settings.Types
{
    public interface IValidationSettings
    {
        /// <summary>
        /// If set to `true`, the program will attempt to recurively 
        /// follow CNAME records present on _acme-challenge subdomains to 
        /// find the final domain the DNS-01 challenge should be handled by.
        /// This allows you to delegate validation of your certificates
        /// to another domain or provider, which can have benefits for 
        /// security or save you the effort of having to move everything 
        /// to a party that supports automation.
        /// </summary>
        bool AllowDnsSubstitution { get; }

        /// <summary>
        /// If set to True, it will cleanup the folder structure
        /// and files it creates under the site for authorization.
        /// </summary>
        bool CleanupFolders { get; }

        /// <summary>
        /// Default plugin to select in the Advanced menu (if
        /// supported for the target), or when nothing is 
        /// specified on the command line.
        /// </summary>
        string DefaultValidation { get; }

        /// <summary>
        /// Default plugin type, e.g. HTTP-01 (default), DNS-01, etc.
        /// </summary>
        string DefaultValidationMode { get; }

        /// <summary>
        /// Disable multithreading for validation
        /// </summary>
        bool DisableMultiThreading { get; }

        /// <summary>
        /// Amount of time to wait for DNS propagation to complete *after* (optional) PreValidation
        /// step has been run.
        /// </summary>
        int DnsPropagationDelay { get; }

        /// <summary>
        /// A comma-separated list of servers to query during DNS 
        /// prevalidation checks to verify whether or not the validation 
        /// record has been properly created and is visible for the world.
        /// These servers will be used to located the actual authoritative 
        /// name servers for the domain. You can use the string [System] to
        /// have the program query your servers default, but note that this 
        /// can lead to prevalidation failures when your Active Directory is 
        /// hosting a private version of the DNS zone for internal use.
        /// </summary>
        IEnumerable<string> DnsServers { get; }

        /// <summary>
        /// Settings for FTP validation
        /// </summary>
        IFtpSettings Ftp { get; }

        /// <summary>
        /// Max number of validations to run in parallel
        /// </summary>
        int ParallelBatchSize { get; }

        /// <summary>
        /// If set to `true`, it will wait until it can verify that the 
        /// validation record has been created and is available before 
        /// beginning DNS validation.
        /// </summary>
        bool PreValidateDns { get; }

        /// <summary>
        /// Add the local DNS server to the list of servers to query during prevalidation
        /// </summary>
        bool PreValidateDnsLocal { get; }

        /// <summary>
        /// Maximum numbers of times to retry DNS pre-validation, while
        /// waiting for the name servers to start providing the expected
        /// answer.
        /// </summary>
        int PreValidateDnsRetryCount { get; }

        /// <summary>
        /// Amount of time in seconds to wait between each retry.
        /// </summary>
        int PreValidateDnsRetryInterval { get; }
    }

    internal class InheritValidationSettings(params IEnumerable<ValidationSettings?> chain) : InheritSettings<ValidationSettings>(chain), IValidationSettings
    {
        public bool AllowDnsSubstitution => Get(x => x.AllowDnsSubstitution) ?? true;
        public bool CleanupFolders => Get(x => x.CleanupFolders) ?? false;
        public string DefaultValidation => Get(x => x.DefaultValidation) ?? "selfhosting";
        public string DefaultValidationMode => Get(x => x.DefaultValidationMode) ?? Constants.DefaultChallengeType;
        public bool DisableMultiThreading => Get(x => x.DisableMultiThreading) ?? true;
        public int DnsPropagationDelay => Get(x => x.DnsPropagationDelay) ?? 0;
        public IEnumerable<string> DnsServers => Get(x => x.DnsServers) ?? [];
        public IFtpSettings Ftp => new InheritFtpSettings(Chain.Select(c => c?.Ftp));
        public int ParallelBatchSize => Get(x => x.ParallelBatchSize) ?? 100;
        public bool PreValidateDns => Get(x => x.PreValidateDns) ?? true;
        public bool PreValidateDnsLocal => Get(x => x.PreValidateDnsLocal) ?? false;
        public int PreValidateDnsRetryCount => Get(x => x.PreValidateDnsRetryCount) ?? 5;
        public int PreValidateDnsRetryInterval => Get(x => x.PreValidateDnsRetryInterval) ?? 30;
    }

    internal class ValidationSettings
    {
        [SettingsValue(
            Description = "Default validation plugin.",
            NullBehaviour = "equivalent to <code>\"selfhosting\"</code>, with <code>\"filesystem\"</code> as backup for unprivileged users.")]
        public string? DefaultValidation { get; set; }

        [SettingsValue(
            Description = "Default validation method.",
            NullBehaviour = "equivalent to <code>\"http-01\"</code>")]
        public string? DefaultValidationMode { get; set; }

        [SettingsValue(
            Default = "true",
            Description = "Disable multithreading features for validation. Inceases runtime but may help to fix bugs caused by race conditions.")]
        public bool? DisableMultiThreading { get; set; }
        
        [SettingsValue(
            Default = "100",
            Description = "Maximum number of validations to run simultaneously. We recommend limiting this to about <code>20</code> to prevent issues like overrunning the maximum size of a DNS response. The default is set to <code>100</code> for backwards compatibility reasons.")]
        public int? ParallelBatchSize { get; set; }
        
        [SettingsValue(
            Default = "true",
            Description = "If set to <code>true</code>, the program will automatically delete file it created after HTTP validation is complete. It will also cleanup the <code>./well-known/acme-challenge</code> folder, if (and only if) there are no other files present.")]
        public bool? CleanupFolders { get; set; }

        [SettingsValue(
            Default = "true",
            Description = "If set to <code>true</code>, it will wait until it can verify that the validation record has been created and is available before beginning DNS validation.")]
        public bool? PreValidateDns { get; set; }

        [SettingsValue(
            Default = "5",
            Description = "Maximum numbers of times to retry DNS pre-validation, while waiting for the name servers to start providing the expected answer.")]
        public int? PreValidateDnsRetryCount { get; set; }

        [SettingsValue(
            Default = "30",
            Description = "AAmount of time in seconds to wait between each retry.")]
        public int? PreValidateDnsRetryInterval { get; set; }

        [SettingsValue(
            Default = "'false'",
            Description = "Normally the program will verify the existence of the TXT record by querying the authoritative DNS servers for the record. Changing this to <code>true</code> will also wait until at least one of the configured <code>DnsServers</code> see the correct value, making the process potentially slower but more robust.")]
        public bool? PreValidateDnsLocal { get; set; }

        [SettingsValue(
            Default = "30",
            Description = "Amount of time in seconds to wait for DNS propagation to complete *after* (optional) PreValidation step has been run. This is required for certain providers to synchronise their world-wide servers, even after the geographically closest ones to simple-acme have already started reflecting the desired records.")]
        public int? DnsPropagationDelay { get; set; }

        [SettingsValue(
            Default = "true",
            Description = "If your goal is to get a certificate for <code>example.com</code> using DNS validation, " +
            "but the DNS service for that domain does not support automation, there is no plugin available for it " +
            "and/or your security policy doesn't allow third party tools like simple-acme to access the DNS " +
            "configuration, then you can set up a <code>CNAME</code> from <code>_acme-challenge.example.com</code> " +
            "to another (sub)domain under your control that doesn't have these limitations. " +
            "\n" +
            "<a href=\"/reference/plugins/validation/dns/acme-dns\">acme-dns</a> is based on this principle, " +
            "but the same trick can be applied to any of the <a href=\"/reference/plugins/validation/dns/\">DNS plugins</a>. " +
            "Set this value to <code>false</code> to disable the feature.",
            Warning = "Note that for the program to understand your DNS setup, the <code>CNAME</code> record will " +
            "have to visible to it. If you have a complicated DNS setup with an internal-facing \"split brain\" that " +
            "is lacking the relevant records, you can let simple-acme use a public DNS server like <code>1.1.1.1</code> " +
            "instead of your system server using the <code>DnsServers</code> setting.")]
        public bool? AllowDnsSubstitution { get; set; }

        [SettingsValue(
            SubType = "host",
            Default = "\"[ \\\"[System]\\\" ]\"",
            Description = "A list of servers to query during DNS prevalidation checksto verify whether or not the " +
            "validation record has been properly created and is visible for the world. These servers will be used to" +
            "locate the actual authoritative name servers for the domain. You can use the string <code>\"[System]\"</code> " +
            "to have the program query the default name servers on your machine.")]
        public List<string>? DnsServers { get; set; }

        public FtpSettings? Ftp { get; set; }
    }
}