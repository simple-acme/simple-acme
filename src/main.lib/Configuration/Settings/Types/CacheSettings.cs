using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.IO;

namespace PKISharp.WACS.Configuration.Settings.Types
{
    public interface ICacheSettings
    {
        /// <summary>
        /// Automatically delete files older than
        /// (DeleteStaleFilesDays) days from the (CachePath). 
        /// Running with default settings, these should 
        /// only be long expired certificates, generated for 
        /// abandoned renewals. However we do advise caution.
        /// </summary>
        bool DeleteStaleFiles { get; }

        /// <summary>
        /// Automatically delete files older than this many days 
        /// from the CertificatePath folder. Running with 
        /// default settings, these should only be long-
        /// expired certificates, generated for abandoned
        /// renewals. However we do advise caution.
        /// </summary>
        int DeleteStaleFilesDays { get; }

        /// <summary>
        /// The path where certificates and request files are 
        /// stored. If not specified or invalid, this defaults 
        /// to (ConfigurationPath)\Certificates. All directories
        /// and subdirectories in the specified path are created 
        /// unless they already exist. If you are using a 
        /// [[Central SSL Store|Store-Plugins#centralssl]], this
        /// can not be set to the same path.
        /// </summary>
        string CachePath { get; }

        /// <summary>
        /// Legacy, SHA256 or Default
        /// </summary>
        string? ProtectionMode { get; }

        /// <summary>
        /// When renewing or re-creating a previously
        /// requested certificate that has the exact 
        /// same set of domain names, the program will 
        /// used a cached version for this many days,
        /// to prevent users from running into rate 
        /// limits while experimenting. Set this to 
        /// a high value if you regularly re-request 
        /// the same certificates, e.g. for a Continuous 
        /// Deployment scenario.
        /// </summary>
        int ReuseDays { get; }
    }

    internal class InheritCacheSettings(ISettings root, params IEnumerable<CacheSettings?> chain) : InheritSettings<CacheSettings>(chain), ICacheSettings
    {
        public bool DeleteStaleFiles => Get(x => x.DeleteStaleFiles) ?? false;
        public int DeleteStaleFilesDays => Get(x => x.DeleteStaleFilesDays) ?? 120;
        public string CachePath { 
            get { 
                var userPath = Get(x => x.Path);
                if (string.IsNullOrWhiteSpace(userPath))
                {
                    return Path.Combine(root.Client.ConfigurationPath, "Certificates");
                }
                return userPath;
            } 
        }
        public string? ProtectionMode => Get(x => x.ProtectionMode);
        public int ReuseDays => Get(x => x.ReuseDays) ?? 1;
    }

    internal class CacheSettings
    {
        public string? Path { get; set; }
        public int? ReuseDays { get; set; }
        public bool? DeleteStaleFiles { get; set; }
        public int? DeleteStaleFilesDays { get; set; }
        public string? ProtectionMode { get; set; }
    }
}