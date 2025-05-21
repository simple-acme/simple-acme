namespace PKISharp.WACS.Configuration.Settings
{
    public class RsaSettings
    {
        /// <summary>
        /// The key size to sign the certificate with. 
        /// Minimum is 2048.
        /// </summary>
        public int? KeyBits { get; set; }
        /// <summary>
        /// CSR signature algorithm, to be picked from 
        /// https://github.com/bcgit/bc-csharp/blob/master/crypto/src/cms/CMSSignedGenerator.cs
        /// </summary>
        public string? SignatureAlgorithm { get; set; }
    }
}