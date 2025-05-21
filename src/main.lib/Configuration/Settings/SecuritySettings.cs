using System;

namespace PKISharp.WACS.Configuration.Settings
{
    public interface ISecuritySettings
    {
        [Obsolete("Use Csr.Ec.CurveName")]
        string? ECCurve { get; }

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
        bool? FriendlyNameDateTimeStamp { get; }

        [Obsolete("Use Store.CertificateStore.PrivateKeyExportable")]
        bool? PrivateKeyExportable { get; }

        [Obsolete("Use Csr.Rsa.KeySize")]
        int? RSAKeyBits { get; }
    }

    internal class SecuritySettings : ISecuritySettings
    {
        public int? RSAKeyBits { get; set; }
        public string? ECCurve { get; set; }
        public bool? PrivateKeyExportable { get; set; }
        public bool EncryptConfig { get; set; }
        public bool? FriendlyNameDateTimeStamp { get; set; }
    }
}