using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types.Csr
{
    public interface IEcSettings
    {
        /// <summary>
        /// The curve to use for EC certificates.
        /// </summary>
        string CurveName { get; }

        /// <summary>
        /// CSR signature algorithm, to be picked from 
        /// https://github.com/bcgit/bc-csharp/blob/master/crypto/src/cms/CMSSignedGenerator.cs
        /// </summary>
        string SignatureAlgorithm { get; }
    }

    internal class InheritEcSettings(params IEnumerable<EcSettings?> chain) : InheritSettings<EcSettings>(chain), IEcSettings
    {
        public string CurveName => Get(x => x.CurveName) ?? "secp384r1";
        public string SignatureAlgorithm => Get(x => x.SignatureAlgorithm) ?? "SHA512withECDSA";
    }

    internal class EcSettings
    {
        public string? CurveName { get; set; }
        public string? SignatureAlgorithm { get; set; }
    }
}