using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Csr
{
    public interface IRsaSettings
    {
        /// <summary>
        /// The key size to sign the certificate with. 
        /// Minimum is 2048.
        /// </summary>
        int KeyBits { get; }

        /// <summary>
        /// CSR signature algorithm, to be picked from 
        /// https://github.com/bcgit/bc-csharp/blob/master/crypto/src/cms/CMSSignedGenerator.cs
        /// </summary>
        string SignatureAlgorithm { get; }
    }

    internal class InheritRsaSettings(params IEnumerable<RsaSettings?> chain) : InheritSettings<RsaSettings>(chain), IRsaSettings
    {
        public int KeyBits => Get(x => x.KeyBits) ?? 3072;
        public string SignatureAlgorithm => Get(x => x.SignatureAlgorithm) ?? "SHA512withRSA";
    }

    internal class RsaSettings
    {
        public int? KeyBits { get; set; }
        public string? SignatureAlgorithm { get; set; }
    }
}