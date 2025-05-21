namespace PKISharp.WACS.Configuration.Settings
{
    public interface ICertificateStoreSettings
    {       
        /// <summary>
        /// The certificate store to save the certificates in. If left empty, 
        /// certificates will be installed either in the WebHosting store, 
        /// or if that is not available, the My store (better known as Personal).
        /// </summary>
        string? DefaultStore { get; }

        /// <summary>
        /// If set to True, it will be possible to export 
        /// the generated certificates from the certificate 
        /// store, for example to move them to another 
        /// server.
        /// </summary>
        bool? PrivateKeyExportable { get; }

        /// <summary>
        /// If set to True, the program will use the "Next-Generation Crypto API" (CNG)
        /// to store private keys, instead of thhe legacy API. Note that this will
        /// make the certificates unusable or behave differently for software that 
        /// only supports the legacy API. For example it will not work in older
        /// versions of Microsoft Exchange and they won't be exportable from IIS,
        /// even if the PrivateKeyExportable setting is true.
        /// </summary>
        bool? UseNextGenerationCryptoApi { get; }
    }

    internal class CertificateStoreSettings : ICertificateStoreSettings
    {
        public string? DefaultStore { get; set; }
        public bool? PrivateKeyExportable { get; set; }
        public bool? UseNextGenerationCryptoApi { get; set; }
    }
}