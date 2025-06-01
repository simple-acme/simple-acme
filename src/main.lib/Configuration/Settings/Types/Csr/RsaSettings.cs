using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types.Csr
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

    public class RsaSettings
    {
        [SettingsValue(
            Default = "3072",
            Description = "The number of bits to use for RSA private keys, ultimately determining the strength of the encryption. Minimum is 2048.")]
        public int? KeyBits { get; set; }

        [SettingsValue(
            Default = "SHA512withRSA",
            Warning = "Note that not all servers will support all types of signatures.",
            Description = "Algorithm to use to sign CSR with RSA public key. Full list of possible options available <a href=\"https://github.com/bcgit/bc-csharp/blob/master/crypto/src/cms/CMSSignedGenerator.cs\">here</a>.")]
        public string? SignatureAlgorithm { get; set; }
    }
}