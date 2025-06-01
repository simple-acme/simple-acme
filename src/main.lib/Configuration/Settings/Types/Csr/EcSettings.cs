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

    public class EcSettings
    {
        [SettingsValue(
            Default = "secp384r1",
            Description = "The curve to use for EC certificates. This should be one of the curves supported by your ACME provider, e.g. secp256r1, secp384r1, or secp521r1.")]
        public string? CurveName { get; set; }

        [SettingsValue(
            Default = "SHA512withECDSA",
            Warning = "Note that not all certificate providers support all types of signatures.",
            Description = "Algorithm to use to sign CSR with EC public key. Full list of possible options available <a href=\"https://github.com/bcgit/bc-csharp/blob/master/crypto/src/cms/CMSSignedGenerator.cs\">here</a>.")]
        public string? SignatureAlgorithm { get; set; }
    }
}