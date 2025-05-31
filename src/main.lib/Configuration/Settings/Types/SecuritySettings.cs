using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types
{
    public interface ISecuritySettings
    {
        /// <summary>
        /// Uses Microsoft Data Protection API to encrypt 
        /// sensitive parts of the configuration, e.g. 
        /// passwords. This may be disabled to share 
        /// the configuration across a cluster of machines.
        /// </summary>
        bool EncryptConfig { get; }

        /// <summary>
        /// Apply a datetimestamp to the friendly name 
        /// of the generated certificates
        /// </summary>
        bool FriendlyNameDateTimeStamp { get; }
    }

    internal class InheritSecuritySettings(params IEnumerable<SecuritySettings?> chain) : InheritSettings<SecuritySettings>(chain), ISecuritySettings
    {
         public bool EncryptConfig => Get(x => x.EncryptConfig) ?? true;
         public bool FriendlyNameDateTimeStamp => Get(x => x.FriendlyNameDateTimeStamp) ?? true;
    }

    internal class SecuritySettings
    {
        [SettingsValue(
            Default = "true",
            Description = "Uses Microsoft Data Protection API to encrypt sensitive parts of the configuration, e.g. passwords. This may be disabled to share the configuration across a cluster of machines.",
            Warning = "Not supported on Linux.")]
        public bool? EncryptConfig { get; set; }

        [SettingsValue(
            Default = "true",
            Description = "Add the issue date and time to the friendly name of requested certificates. This feature should be disabled if you require full control over the final certificate friendly name.")]
        public bool? FriendlyNameDateTimeStamp { get; set; }

        [SettingsValue(Hidden = true)]
        public bool? PrivateKeyExportable { get; set; }

        [SettingsValue(Hidden = true)]
        public int? RSAKeyBits { get; set; }

        [SettingsValue(Hidden = true)]
        public string? ECCurve { get; set; }
    }
}