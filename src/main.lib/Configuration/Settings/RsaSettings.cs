namespace PKISharp.WACS.Configuration.Settings
{
    public interface IRsaSettings
    {
        /// <summary>
        /// The key size to sign the certificate with. 
        /// Minimum is 2048.
        /// </summary>
        int? KeyBits { get; }

        /// <summary>
        /// CSR signature algorithm, to be picked from 
        /// https://github.com/bcgit/bc-csharp/blob/master/crypto/src/cms/CMSSignedGenerator.cs
        /// </summary>
        string? SignatureAlgorithm { get; }
    }

    internal class RsaSettings : IRsaSettings
    {
        public int? KeyBits { get; set; }
        public string? SignatureAlgorithm { get; set; }
    }
}