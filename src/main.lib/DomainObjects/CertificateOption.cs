namespace PKISharp.WACS.DomainObjects
{
    /// <summary>
    /// Represents a certificate
    /// </summary>
    internal class CertificateOption(ICertificateInfo withPrivateKey, ICertificateInfo withoutPrivateKey)
    {
        public ICertificateInfo WithPrivateKey { get; set; } = withPrivateKey;
        public ICertificateInfo WithoutPrivateKey { get; set; } = withoutPrivateKey;
    }
}
