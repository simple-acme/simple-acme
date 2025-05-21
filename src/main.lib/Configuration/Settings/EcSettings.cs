namespace PKISharp.WACS.Configuration.Settings
{
    public interface IEcSettings
    {
        /// <summary>
        /// The curve to use for EC certificates.
        /// </summary>
        string? CurveName { get; }

        /// <summary>
        /// CSR signature algorithm, to be picked from 
        /// https://github.com/bcgit/bc-csharp/blob/master/crypto/src/cms/CMSSignedGenerator.cs
        /// </summary>
        string? SignatureAlgorithm { get; }
    }

    internal class EcSettings : IEcSettings
    {
        public string? CurveName { get; set; }
        public string? SignatureAlgorithm { get; set; }
    }
}