namespace PKISharp.WACS.Configuration.Settings
{
    public class EcSettings
    {
        /// <summary>
        /// The curve to use for EC certificates.
        /// </summary>
        public string? CurveName { get; set; }
        /// <summary>
        /// CSR signature algorithm, to be picked from 
        /// https://github.com/bcgit/bc-csharp/blob/master/crypto/src/cms/CMSSignedGenerator.cs
        /// </summary>
        public string? SignatureAlgorithm { get; set; }
    }
}