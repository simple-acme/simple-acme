using System;

namespace PKISharp.WACS.Configuration.Settings
{
    public class SecuritySettings
    {
        [Obsolete("Use Csr.Rsa.KeySize")]
        public int? RSAKeyBits { get; set; }

        [Obsolete("Use Csr.Ec.CurveName")]
        public string? ECCurve { get; set; }

        [Obsolete("Use Store.CertificateStore.PrivateKeyExportable")]
        public bool? PrivateKeyExportable { get; set; }

        /// <summary>
        /// Uses Microsoft Data Protection API to encrypt 
        /// sensitive parts of the configuration, e.g. 
        /// passwords. This may be disabled to share 
        /// the configuration across a cluster of machines.
        /// </summary>
        public bool EncryptConfig { get; set; }
        /// <summary>
        /// Apply a datetimestamp to the friendly name 
        /// of the generated certificates
        /// </summary>
        public bool? FriendlyNameDateTimeStamp { get; set; }
    }
}