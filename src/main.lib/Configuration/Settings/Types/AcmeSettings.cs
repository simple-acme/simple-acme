using System;
using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types
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
        /// Preferred ACME profile to use for the order.
        /// https://letsencrypt.org/2025/01/09/acme-profiles/
        /// </summary>
        string? CertificateProfile { get; }

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

    internal class InheritAcmeSettings(params IEnumerable<AcmeSettings?> chain) : InheritSettings<AcmeSettings>(chain), IAcmeSettings
    {
        public Uri? DefaultBaseUri => Get(x => x.DefaultBaseUri);
        public Uri? DefaultBaseUriImport => Get(x => x.DefaultBaseUriImport);
        public Uri? DefaultBaseUriTest => Get(x => x.DefaultBaseUriTest);
        public int MaxDomains => Get(x => x.MaxDomains) ?? 100;
        public bool PostAsGet => Get(x => x.PostAsGet) ?? true;
        public string? PreferredIssuer => Get(x => x.PreferredIssuer);
        public string? CertificateProfile => Get(x => x.CertificateProfile);
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

    public class AcmeSettings
    {
        [SettingsValue(
            Default = "https://acme-v02.api.letsencrypt.org/",
            Description = "Default ACME endpoint to use when none is specified with the command line. The client will attempt to get the service directory from both the literal uri provided and the <code>/directory</code> path under it (which is the convention used by Let's Encrypt, and therefor done for backwards compatibility reasons).")]
        public Uri? DefaultBaseUri { get; set; }

        [SettingsValue(
            Default = "https://acme-staging-v02.api.letsencrypt.org/",
            Description = "Default ACME endpoint to use when none is specified with the command line and the <code>‑‑test</code> switch is activated.")]
        public Uri? DefaultBaseUriTest { get; set; }
        
        [SettingsValue(
           Default = "https://acme-v01.api.letsencrypt.org/",
           Description = "Default ACMEv1 endpoint to import renewal settings from.")]
        public Uri? DefaultBaseUriImport { get; set; }

        [SettingsValue(
            Default = "true",
            Description = "Use POST-as-GET mode as defined in <a href=\"https://datatracker.ietf.org/doc/html/rfc8555#section-6.3\">RFC8555 section 6.3</a>, as required by Let's Encrypt since November 2020.")]
        public bool? PostAsGet { get; set; }
        
        [SettingsValue(
           Default = "true",
           Description = "Set this to <code>false</code> to disable certificate validation of the ACME endpoint.",
           Warning = "Note that setting this to <code>false</code> is a security risk, it's only intended to connect to internal/private ACME servers with self-signed certificates.")]
        public bool? ValidateServerCertificate { get; set; }

        [SettingsValue(
            Default = "15",
            Description = "Maximum numbers of times to refresh validation and order status, while waiting for the ACME server to complete its tasks.")]
        public int? RetryCount { get; set; }
        
        [SettingsValue(
            Default = "5",
            Description = "Amount of time in seconds to wait for each retry.")]
        public int? RetryInterval { get; set; }

        [SettingsValue(
            Description = "In some exceptional cases an ACME service will offer multiple certificates signed by different root authorities. This setting can be used to give a preference. I.e. <code>\"ISRG Root X1\"</code> can be used to prefer Let's Encrypt self-signed chain over the backwards compatible <code>\"DST Root CA X3\"</code>.",
            Warning = "Note that this only really works for Apache and other software that uses <code>.pem</code> files to store certificates. Windows has its own opinions about how chains should be built that are difficult to influence. For maximum compatibility with legacy clients we recommend using an alternative provider like <a href=\"https://zerossl.com\">ZeroSSL</a>.")]
        public string? PreferredIssuer { get; set; }

        [SettingsValue(
            Description = "Choose which <a href=\"https://letsencrypt.org/2025/01/09/acme-profiles/\">Certificate Profile</a> should be chosen if the server offers more than one of them.")]
        public string? CertificateProfile { get; set; }

        [SettingsValue(
            Default = "100",
            Description = "Maximum number of host names that can be included in a single certificate.",
            Warning = "The client cannot override limits imposed by the server. Increase this value only if you're sure that your server supports it.")]
        public int? MaxDomains { get; set; }

        [SettingsValue(
            SubType = "uri",
            Default = "https://publicsuffix.org/list/public_suffix_list.dat",
            Description = "Link from where the current version of the public suffix list is downloaded. This may be set to <code>\"\"</code> (empty string) to stop the program from updating the list, or to a custom location to provide your own version.",
            Warning = "Using an out-of-date or invalid list may cause errors, only change this if you know what you're doing!")]
        public string? PublicSuffixListUri { get; set; }
    }
}