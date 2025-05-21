using System;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Configuration.Settings
{
    public class AcmeSettings
    {
        /// <summary>
        /// Selected BaseUri
        /// </summary>
        [JsonIgnore]
        public Uri BaseUri { get; set; } = new Uri("https://localhost");

        /// <summary>
        /// Default ACMEv2 endpoint to use when none 
        /// is specified with the command line.
        /// </summary>
        public Uri? DefaultBaseUri { get; set; }
        /// <summary>
        /// Default ACMEv2 endpoint to use when none is specified 
        /// with the command line and the --test switch is
        /// activated.
        /// </summary>
        public Uri? DefaultBaseUriTest { get; set; }
        /// <summary>
        /// Default ACMEv1 endpoint to import renewal settings from.
        /// </summary>
        public Uri? DefaultBaseUriImport { get; set; }
        /// <summary>
        /// Use POST-as-GET request mode
        /// </summary>
        public bool PostAsGet { get; set; }
        /// <summary>
        /// Validate the server certificate
        /// </summary>
        public bool? ValidateServerCertificate { get; set; }
        /// <summary>
        /// Number of times wait for the ACME server to 
        /// handle validation and order processing
        /// </summary>
        public int RetryCount { get; set; } = 4;
        /// <summary>
        /// Amount of time (in seconds) to wait each 
        /// retry for the validation handling and order
        /// processing
        /// </summary>
        public int RetryInterval { get; set; } = 2;
        /// <summary>
        /// If there are alternate certificate, select 
        /// which issuer is preferred
        /// </summary>
        public string? PreferredIssuer { get; set; }
        /// <summary>
        /// Maximum number of domains supported
        /// </summary>
        public int? MaxDomains { get; set; }
        /// <summary>
        /// Location of the public suffix list
        /// </summary>
        public Uri? PublicSuffixListUri { get; set; }
    }
}