using PKISharp.WACS.Configuration.Settings.Validation;
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Configuration.Settings
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

    internal class InheritValidationSettings(params IEnumerable<ValidationSettings> chain) : InheritSettings<ValidationSettings>(chain), IValidationSettings
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
        public string? DefaultValidation { get; set; }
        public string? DefaultValidationMode { get; set; }
        public bool? DisableMultiThreading { get; set; }
        public int? ParallelBatchSize { get; set; }
        public bool? CleanupFolders { get; set; }
        public bool? PreValidateDns { get; set; } = true;
        public int? PreValidateDnsRetryCount { get; set; }
        public int? PreValidateDnsRetryInterval { get; set; }
        public bool? PreValidateDnsLocal { get; set; }
        public int? DnsPropagationDelay { get; set; }
        public bool? AllowDnsSubstitution { get; set; }
        public List<string>? DnsServers { get; set; }
        public FtpSettings? Ftp { get; set; }
    }
}