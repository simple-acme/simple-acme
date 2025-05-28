using System.Collections.Generic;
using System.Configuration;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace PKISharp.WACS.Configuration.Settings.Types.Store
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
        bool PrivateKeyExportable { get; }

        /// <summary>
        /// If set to True, the program will use the "Next-Generation Crypto API" (CNG)
        /// to store private keys, instead of thhe legacy API. Note that this will
        /// make the certificates unusable or behave differently for software that 
        /// only supports the legacy API. For example it will not work in older
        /// versions of Microsoft Exchange and they won't be exportable from IIS,
        /// even if the PrivateKeyExportable setting is true.
        /// </summary>
        bool UseNextGenerationCryptoApi { get; }
    }

    internal class InheritCertificateStoreSettings(params IEnumerable<CertificateStoreSettings?> chain) : InheritSettings<CertificateStoreSettings>(chain), ICertificateStoreSettings
    {
        public string? DefaultStore => Get(x => x.DefaultStore);
        public bool PrivateKeyExportable => Get(x => x.PrivateKeyExportable) ?? false;
        public bool UseNextGenerationCryptoApi => Get(x => x.UseNextGenerationCryptoApi) ?? false;
    }

    internal class CertificateStoreSettings
    {
        [SettingsValue(
            Description = "The name of the certificate store to save the certificates in.", 
            NullBehaviour = "certificates will be installed either in the <code>\"WebHosting\"</code> store, " +
            "or if that is not available, the <code>\"My\"</code> store (better known in the Microsoft " +
            "Management Console as as <code>\"Personal\"</code>)")]
        public string? DefaultStore { get; set; }

        [SettingsValue(
            Default = "'false'",
            Description = "If set to <code>true</code>, private keys stored in the Windows Certificate Store " +
            "will be marked as exportable, allowing you to transfer them to other computers.",
            Warning = "Note that this setting doesn't apply retroactively, but only to certificates issued " +
            "from the moment that setting has changed. For tips about migration please refer to " +
            "<a href=\"/manual/migration\">this page</a>.")]
        public bool? PrivateKeyExportable { get; set; }

        [SettingsValue(
            Default = "'false'",
            Description = "\"If set to <code>true</code>, the program will use the " +
            "<a href=\\\"https://learn.microsoft.com/en-us/windows/win32/seccng/about-cng\\\">Cryptography API: " +
            "Next Generation (CNG)</a> to handle private keys, instead of the legacy CryptoAPI.\"",
            Warning = "\"Note that enabling this option may make the certificates unusable or behave differently " +
            "in subtle ways for software that only supports or assumes the key to exist in CryptoAPI. For example:" +
            "<ul>" +
            "<li>It will not (fully) work for older versions of Microsoft Exchange (and this might only become apparent when installing a service pack)</li>" +
            "<li>It won't be exportable from the IIS Manager, even if <code>PrivateKeyExportable</code> is <code>true</code> (though it will be exportable from MMC).</li>" +
            "<li>The arguments <code>--acl-read</code> and <code>--acl-fullcontrol</code> used to set key permissions may not work on all versions of Windows</li>" +
            "</ul>\"")]
        public bool? UseNextGenerationCryptoApi { get; set; }
    }
}