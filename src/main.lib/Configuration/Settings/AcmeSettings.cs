using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Configuration.Settings
{
    public interface IAcmeSettings
    {       
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
        int MaxDomains { get; }        

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
        bool ValidateServerCertificate { get; }
    }

    internal class InheritAcmeSettings(params IEnumerable<AcmeSettings> chain) : InheritSettings<AcmeSettings>(chain), IAcmeSettings
    {
        public Uri BaseUri => throw new NotImplementedException();
        public Uri? DefaultBaseUri => Get(x => x.DefaultBaseUri);
        public Uri? DefaultBaseUriImport => Get(x => x.DefaultBaseUriImport);
        public Uri? DefaultBaseUriTest => Get(x => x.DefaultBaseUriTest);
        public int MaxDomains => Get(x => x.MaxDomains) ?? 100;
        public bool PostAsGet => Get(x => x.PostAsGet) ?? true;
        public string? PreferredIssuer => Get(x => x.PreferredIssuer) ?? null;
        public Uri? PublicSuffixListUri
        {
            get
            {
                var uri = Get(x => x.PublicSuffixListUri);
                if (uri == "")
                {
                    return null;
                }
                if (uri == null)
                {
                    return new Uri("https://publicsuffix.org/list/public_suffix_list.dat");
                }
                return new Uri(uri);
            }
        }
        public int RetryCount => Get(x => x.RetryCount) ?? 4;
        public int RetryInterval => Get(x => x.RetryInterval) ?? 2;
        public bool ValidateServerCertificate => Get(x => x.ValidateServerCertificate) ?? true;
    }

    internal class AcmeSettings
    {
        public Uri? DefaultBaseUri { get; set; }
        public Uri? DefaultBaseUriTest { get; set; }
        public Uri? DefaultBaseUriImport { get; set; }
        public bool? PostAsGet { get; set; }
        public bool? ValidateServerCertificate { get; set; }
        public int? RetryCount { get; set; }
        public int? RetryInterval { get; set; }
        public string? PreferredIssuer { get; set; }
        public int? MaxDomains { get; set; }
        public string? PublicSuffixListUri { get; set; }
    }
}