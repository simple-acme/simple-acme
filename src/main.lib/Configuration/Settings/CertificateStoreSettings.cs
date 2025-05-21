namespace PKISharp.WACS.Configuration.Settings
{
    public class CertificateStoreSettings
    {
        /// <summary>
        /// The certificate store to save the certificates in. If left empty, 
        /// certificates will be installed either in the WebHosting store, 
        /// or if that is not available, the My store (better known as Personal).
        /// </summary>
        public string? DefaultStore { get; set; }
        /// <summary>
        /// If set to True, it will be possible to export 
        /// the generated certificates from the certificate 
        /// store, for example to move them to another 
        /// server.
        /// </summary>
        public bool? PrivateKeyExportable { get; set; }
        /// <summary>
        /// If set to True, the program will use the "Next-Generation Crypto API" (CNG)
        /// to store private keys, instead of thhe legacy API. Note that this will
        /// make the certificates unusable or behave differently for software that 
        /// only supports the legacy API. For example it will not work in older
        /// versions of Microsoft Exchange and they won't be exportable from IIS,
        /// even if the PrivateKeyExportable setting is true.
        /// </summary>
        public bool? UseNextGenerationCryptoApi { get; set; }
    }
}