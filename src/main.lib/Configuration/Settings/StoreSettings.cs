using System;

namespace PKISharp.WACS.Configuration.Settings
{
    public interface IStoreSettings
    {
        /// <summary>
        /// Settings for the CentralSsl plugin
        /// </summary>
        ICentralSslSettings CentralSsl { get; }

        /// <summary>
        /// Settings for the CentralSsl plugin
        /// </summary>
        ICertificateStoreSettings CertificateStore { get; }

        [Obsolete("Use CentralSsl.DefaultPassword instead")]
        string? DefaultCentralSslPfxPassword { get; }

        [Obsolete("Use CentralSsl.DefaultStore instead")]
        string? DefaultCentralSslStore { get; }

        [Obsolete("Use CertificateStore.DefaultStore instead")]
        string? DefaultCertificateStore { get; }

        [Obsolete("Use PemFiles.DefaultPath instead")]
        string? DefaultPemFilesPath { get; }

        /// <summary>
        /// Default plugin(s) to select 
        /// </summary>
        string? DefaultStore { get; }

        /// <summary>
        /// Settings for the P7bFile plugin
        /// </summary>
        IP7bFileSettings P7bFile { get; }

        /// <summary>
        /// Settings for the PemFiles plugin
        /// </summary>
        IPemFilesSettings PemFiles { get; }

        /// <summary>
        /// Settings for the PfxFile plugin
        /// </summary>
        IPfxFileSettings PfxFile { get; }
    }

    internal class StoreSettings : IStoreSettings
    {
        public string? DefaultStore { get; set; }
        public string? DefaultCertificateStore { get; set; }
        public string? DefaultCentralSslStore { get; set; }
        public string? DefaultCentralSslPfxPassword { get; set; }
        public string? DefaultPemFilesPath { get; set; }
        public CertificateStoreSettings CertificateStore { get; set; } = new CertificateStoreSettings();
        public CentralSslSettings CentralSsl { get; set; } = new CentralSslSettings();
        public PemFilesSettings PemFiles { get; set; } = new PemFilesSettings();
        public PfxFileSettings PfxFile { get; set; } = new PfxFileSettings();
        public P7bFileSettings P7bFile { get; set; } = new P7bFileSettings();

        ICentralSslSettings IStoreSettings.CentralSsl => CentralSsl;
        ICertificateStoreSettings IStoreSettings.CertificateStore => CertificateStore;
        IP7bFileSettings IStoreSettings.P7bFile => P7bFile;
        IPemFilesSettings IStoreSettings.PemFiles => PemFiles;
        IPfxFileSettings IStoreSettings.PfxFile => PfxFile;
    }
}