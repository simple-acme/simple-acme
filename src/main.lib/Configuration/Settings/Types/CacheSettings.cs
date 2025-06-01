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

    public class CacheSettings
    {
        [SettingsValue(SubType = "path",
            NullBehaviour = "defaults to <code>{Client.ConfigurationPath}\\Certificates</code>",
            Description = "The path where certificates, request files and private keys are cached. If you are using <a href=\"/reference/plugins/store/centralssl\">CentralSsl</a>, this can not be set to the same path.")]
        public string? Path { get; set; }

        [SettingsValue(
            Default = "1",
            Description = "When renewing or re-creating a previously requested certificate that " +
            "has the exact same set of domain names, the program will used a cached version for " +
            "this many days, to prevent users from running into " +
            "<a href=\"https://letsencrypt.org/docs/rate-limits/\">rate limits</a> while experimenting. " +
            "Set this to a high value if you regularly re-request the same certificates, e.g. for a " +
            "Continuous Deployment scenario. " +
            "\n" +
            "Setting this to <code>0</code> will not entirely disable the cache (the program also " +
            "needs the files for different reasons), but it will prevent the files from " +
            "being used for renewals and will also ensure that no private key material " +
            "is stored in the cache, unless specifically requested by <code>‑‑reuse-privatekey</code>.")]
        public int? ReuseDays { get; set; }

        [SettingsValue(Default = "false",
            Description = "Automatically delete files older than <code>DeleteStaleFileDays</code> " +
            "many days from the folder <code>{Cache.Path}</code>. Running with default settings, " +
            "these should only be long-expired certificates, generated for abandoned renewals.")]
        public bool? DeleteStaleFiles { get; set; }

        [SettingsValue(Default = "120",
            Description = "This value should be increased if you are working with long-lived " +
            "certificates and enable <code>DeleteStaleFiles</code>.")]
        public int? DeleteStaleFilesDays { get; set; }

        [SettingsValue(
            Default = "default",
            SubType = "protectionmode",
            Description = "Determines how the <code>.pfx</code> files in the cache are encrypted.")]
        public string? ProtectionMode { get; set; }
    }
}