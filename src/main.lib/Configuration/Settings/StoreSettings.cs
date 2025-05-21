using System;

namespace PKISharp.WACS.Configuration.Settings
{
    public class StoreSettings
    {
        /// <summary>
        /// Default plugin(s) to select 
        /// </summary>
        public string? DefaultStore { get; set; }

        [Obsolete("Use CertificateStore.DefaultStore instead")]
        public string? DefaultCertificateStore { get; set; }
        [Obsolete("Use CentralSsl.DefaultStore instead")]
        public string? DefaultCentralSslStore { get; set; }
        [Obsolete("Use CentralSsl.DefaultPassword instead")]
        public string? DefaultCentralSslPfxPassword { get; set; }
        [Obsolete("Use PemFiles.DefaultPath instead")]
        public string? DefaultPemFilesPath { get; set; }

        /// <summary>
        /// Settings for the CentralSsl plugin
        /// </summary>
        public CertificateStoreSettings CertificateStore { get; set; } = new CertificateStoreSettings();

        /// <summary>
        /// Settings for the CentralSsl plugin
        /// </summary>
        public CentralSslSettings CentralSsl { get; set; } = new CentralSslSettings();

        /// <summary>
        /// Settings for the PemFiles plugin
        /// </summary>
        public PemFilesSettings PemFiles { get; set; } = new PemFilesSettings();

        /// <summary>
        /// Settings for the PfxFile plugin
        /// </summary>
        public PfxFileSettings PfxFile { get; set; } = new PfxFileSettings();

        /// <summary>
        /// Settings for the P7bFile plugin
        /// </summary>
        public P7bFileSettings P7bFile { get; set; } = new P7bFileSettings();
    }
}