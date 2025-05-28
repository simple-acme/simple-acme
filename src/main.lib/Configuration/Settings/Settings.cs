using PKISharp.WACS.Configuration.Settings.Types;
using PKISharp.WACS.Configuration.Settings.Types.Csr;
using PKISharp.WACS.Configuration.Settings.Types.Store;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Configuration.Settings
{
    [JsonSerializable(typeof(Settings))]
    internal partial class SettingsJson : JsonSerializerContext
    {
        public static SettingsJson Insensitive => new(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        });
    }

    /// <summary>
    /// All settings
    /// </summary>
    internal class Settings
    {
        public ClientSettings? Client { get; set; }
        public UiSettings? UI { get; set; }
        public AcmeSettings? Acme { get; set; }
        public ExecutionSettings? Execution { get; set; }
        public ProxySettings? Proxy { get; set; }
        public CacheSettings? Cache { get; set; }
        public ScheduledTaskSettings? ScheduledTask { get; set; }
        public NotificationSettings? Notification { get; set; }
        public SecuritySettings? Security { get; set; }
        public ScriptSettings? Script { get; set; }
        public SourceSettings? Target { get; set; }
        public SourceSettings? Source { get; set; }
        public ValidationSettings? Validation { get; set; }
        public OrderSettings? Order { get; set; }
        public CsrSettings? Csr { get; set; }
        public StoreSettings? Store { get; set; }
        public InstallationSettings? Installation { get; set; }
        public SecretsSettings? Secrets { get; set; }

        /// <summary>
        /// Load settings from disk
        /// </summary>
        /// <param name="useFile"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static Settings Load(FileInfo useFile)
        {
            using var fs = useFile.OpenRead();
            var newSettings = JsonSerializer.Deserialize(fs, SettingsJson.Insensitive.Settings) ?? throw new Exception($"Unable to deserialize {useFile.FullName}");

            // Migrate old-style settings to new-style settings
            if (newSettings.Target?.DefaultTarget != null)
            {
                newSettings.Source ??= new SourceSettings();
                newSettings.Source.DefaultSource ??= newSettings.Target.DefaultTarget;
            }
            if (newSettings.Store?.DefaultPemFilesPath != null)
            {
                newSettings.Store.PemFiles ??= new PemFilesSettings();
                newSettings.Store.PemFiles.DefaultPath ??= newSettings.Store.DefaultPemFilesPath;
            }
            if (newSettings.Store?.DefaultCentralSslStore != null)
            {
                newSettings.Store.CentralSsl ??= new CentralSslSettings();
                newSettings.Store.CentralSsl.DefaultPath ??= newSettings.Store.DefaultCentralSslStore;
            }
            if (newSettings.Store?.DefaultCentralSslPfxPassword != null)
            {
                newSettings.Store.CentralSsl ??= new CentralSslSettings();
                newSettings.Store.CentralSsl.DefaultPassword ??= newSettings.Store.DefaultCentralSslPfxPassword;
            }
            if (newSettings.Store?.DefaultCertificateStore != null)
            {
                newSettings.Store.CertificateStore ??= new CertificateStoreSettings();
                newSettings.Store.CertificateStore.DefaultStore ??= newSettings.Store.DefaultCertificateStore;
            }
            if (newSettings.Security?.ECCurve != null)
            {
                newSettings.Csr ??= new CsrSettings();
                newSettings.Csr.Ec ??= new EcSettings();
                newSettings.Csr.Ec.CurveName ??= newSettings.Security.ECCurve;
            }
            if (newSettings.Security?.PrivateKeyExportable != null)
            {
                newSettings.Store ??= new StoreSettings();
                newSettings.Store.CertificateStore ??= new CertificateStoreSettings();
                newSettings.Store.CertificateStore.PrivateKeyExportable ??= newSettings.Security.PrivateKeyExportable;
            }
            if (newSettings.Security?.RSAKeyBits != null)
            {
                newSettings.Csr ??= new CsrSettings();
                newSettings.Csr.Rsa ??= new RsaSettings();
                newSettings.Csr.Rsa.KeyBits ??= newSettings.Security.RSAKeyBits;
            }
            return newSettings;
        }
    }

}