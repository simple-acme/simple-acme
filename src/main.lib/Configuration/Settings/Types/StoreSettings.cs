using PKISharp.WACS.Configuration.Settings.Types.Store;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Configuration.Settings.Types
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
        /// <summary>
        /// Default plugin(s) to select 
        /// </summary>
        string DefaultStore { get; }
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

    internal class InheritStoreSettings(params IEnumerable<StoreSettings?> chain) : InheritSettings<StoreSettings>(chain), IStoreSettings
    {
        public ICentralSslSettings CentralSsl => new InheritCentralSslSettings(Chain.Select(c => c?.CentralSsl));
        public ICertificateStoreSettings CertificateStore => new InheritCertificateStoreSettings(Chain.Select(c => c?.CertificateStore));
        public string DefaultStore => Get(x => x.DefaultStore) ?? (OperatingSystem.IsWindows() ? Plugins.StorePlugins.CertificateStore.Trigger : Plugins.StorePlugins.PemFiles.Trigger);
        public IP7bFileSettings P7bFile => new InheritP7bFileSettings(Chain.Select(c => c?.P7bFile));
        public IPemFilesSettings PemFiles => new InheritPemFilesSettings(Chain.Select(c => c?.PemFiles));
        public IPfxFileSettings PfxFile => new InheritPfxFileSettings(Chain.Select(c => c?.PfxFile));
    }

    internal class StoreSettings
    {
        [SettingsValue(
            Description = "Default store plugin(s)", 
            Tip = "This may be a comma separated value for multiple default store plugins.",
            NullBehaviour = "equivalent to <code>\"certificatestore\"</code> on Windows and <code>\"pemfiles\"</code> on other platforms")]
        public string? DefaultStore { get; set; }

        [SettingsValue(Split = true)]
        public CertificateStoreSettings? CertificateStore { get; set; }

        [SettingsValue(Split = true)]
        public CentralSslSettings? CentralSsl { get; set; }

        [SettingsValue(Split = true)]
        public PemFilesSettings? PemFiles { get; set; }

        [SettingsValue(Split = true)]
        public PfxFileSettings? PfxFile { get; set; }

        [SettingsValue(Split = true)]
        public P7bFileSettings? P7bFile { get; set; }

        [SettingsValue(Hidden = true)]
        public string? DefaultCertificateStore { get; set; }
        [SettingsValue(Hidden = true)]
        public string? DefaultCentralSslStore { get; set; }
        [SettingsValue(Hidden = true)]
        public string? DefaultCentralSslPfxPassword { get; set; }
        [SettingsValue(Hidden = true)]
        public string? DefaultPemFilesPath { get; set; }
    }
}