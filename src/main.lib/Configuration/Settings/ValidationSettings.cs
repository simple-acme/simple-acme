using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings
{
    public class ValidationSettings
    {
        /// <summary>
        /// Default plugin to select in the Advanced menu (if
        /// supported for the target), or when nothing is 
        /// specified on the command line.
        /// </summary>
        public string? DefaultValidation { get; set; }

        /// <summary>
        /// Default plugin type, e.g. HTTP-01 (default), DNS-01, etc.
        /// </summary>
        public string? DefaultValidationMode { get; set; }

        /// <summary>
        /// Disable multithreading for validation
        /// </summary>
        public bool? DisableMultiThreading { get; set; }

        /// <summary>
        /// Max number of validations to run in parallel
        /// </summary>
        public int? ParallelBatchSize { get; set; }

        /// <summary>
        /// If set to True, it will cleanup the folder structure
        /// and files it creates under the site for authorization.
        /// </summary>
        public bool CleanupFolders { get; set; }
        /// <summary>
        /// If set to `true`, it will wait until it can verify that the 
        /// validation record has been created and is available before 
        /// beginning DNS validation.
        /// </summary>
        public bool PreValidateDns { get; set; } = true;
        /// <summary>
        /// Maximum numbers of times to retry DNS pre-validation, while
        /// waiting for the name servers to start providing the expected
        /// answer.
        /// </summary>
        public int PreValidateDnsRetryCount { get; set; } = 5;
        /// <summary>
        /// Amount of time in seconds to wait between each retry.
        /// </summary>
        public int PreValidateDnsRetryInterval { get; set; } = 30;
        /// <summary>
        /// Add the local DNS server to the list of servers to query during prevalidation
        /// </summary>
        public bool? PreValidateDnsLocal { get; set; } = false;
        /// <summary>
        /// Amount of time to wait for DNS propagation to complete *after* (optional) PreValidation
        /// step has been run.
        /// </summary>
        public int DnsPropagationDelay { get; set; } = 0;
        /// <summary>
        /// If set to `true`, the program will attempt to recurively 
        /// follow CNAME records present on _acme-challenge subdomains to 
        /// find the final domain the DNS-01 challenge should be handled by.
        /// This allows you to delegate validation of your certificates
        /// to another domain or provider, which can have benefits for 
        /// security or save you the effort of having to move everything 
        /// to a party that supports automation.
        /// </summary>
        public bool AllowDnsSubstitution { get; set; } = true;
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
        public List<string>? DnsServers { get; set; }
        /// <summary>
        /// Settings for FTP validation
        /// </summary>
        public FtpSettings? Ftp { get; set; }
    }
}