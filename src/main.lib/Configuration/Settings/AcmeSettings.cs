using System;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Configuration.Settings
{
    public interface IAcmeSettings
    {       
        /// <summary>
        /// Selected BaseUri
        /// </summary>
        Uri BaseUri { get; }

        /// <summary>
        /// Default ACMEv2 endpoint to use when none 
        /// is specified with the command line.
        /// </summary>
        Uri? DefaultBaseUri { get; }

        /// <summary>
        /// Default ACMEv1 endpoint to import renewal settings from.
        /// </summary>
        Uri? DefaultBaseUriImport { get; }

        /// <summary>
        /// Default ACMEv2 endpoint to use when none is specified 
        /// with the command line and the --test switch is
        /// activated.
        /// </summary>
        Uri? DefaultBaseUriTest { get; }

        /// <summary>
        /// Maximum number of domains supported
        /// </summary>
        int? MaxDomains { get; }        

        /// <summary>                        
        /// Use POST-as-GET request mode
        /// </summary>
        bool PostAsGet { get; }

        /// <summary>
        /// If there are alternate certificate, select 
        /// which issuer is preferred
        /// </summary>
        string? PreferredIssuer { get; }

        /// <summary>
        /// Location of the public suffix list
        /// </summary>
        Uri? PublicSuffixListUri { get; }

        /// <summary>
        /// Number of times wait for the ACME server to 
        /// handle validation and order processing
        /// </summary>
        int RetryCount { get; }

        /// <summary>
        /// Amount of time (in seconds) to wait each 
        /// retry for the validation handling and order
        /// processing
        /// </summary>
        int RetryInterval { get; }   
        
        /// <summary>
        /// Validate the server certificate
        /// </summary>
        bool? ValidateServerCertificate { get; }
    }

    internal class AcmeSettings : IAcmeSettings
    {
        [JsonIgnore]
        public Uri BaseUri { get; set; } = new Uri("https://localhost");
        public Uri? DefaultBaseUri { get; set; }
        public Uri? DefaultBaseUriTest { get; set; }
        public Uri? DefaultBaseUriImport { get; set; }
        public bool PostAsGet { get; set; }
        public bool? ValidateServerCertificate { get; set; }
        public int RetryCount { get; set; } = 4;
        public int RetryInterval { get; set; } = 2;
        public string? PreferredIssuer { get; set; }
        public int? MaxDomains { get; set; }
        public Uri? PublicSuffixListUri { get; set; }
    }
}