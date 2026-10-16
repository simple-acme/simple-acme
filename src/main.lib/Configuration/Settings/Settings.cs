using PKISharp.WACS.Configuration.Settings.Types;
using PKISharp.WACS.Configuration.Settings.Types.Csr;
using PKISharp.WACS.Configuration.Settings.Types.Notification;
using PKISharp.WACS.Configuration.Settings.Types.Store;
using PKISharp.WACS.Configuration.Settings.Types.Validation;
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
    public class Settings
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
        [SettingsValue(Hidden = true)]
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

            if (newSettings.Notification?.EmailOnSuccess != null)
            {
                newSettings.Notification.Email ??= new EmailSettings();
                newSettings.Notification.Email.NotifyOnSuccess ??= newSettings.Notification.EmailOnSuccess;
            }
            if (newSettings.Notification?.ReceiverAddresses != null)
            {
                newSettings.Notification.Email ??= new EmailSettings();
                newSettings.Notification.Email.ReceiverAddresses ??= newSettings.Notification.ReceiverAddresses;
            }
            if (newSettings.Notification?.SenderAddress != null)
            {
                newSettings.Notification.Email ??= new EmailSettings();
                newSettings.Notification.Email.SenderAddress ??= newSettings.Notification.SenderAddress;
            }
            if (newSettings.Notification?.SenderName != null)
            {
                newSettings.Notification.Email ??= new EmailSettings();
                newSettings.Notification.Email.SenderName ??= newSettings.Notification.SenderName;
            }
            if (newSettings.Notification?.SmtpPassword != null)
            {
                newSettings.Notification.Email ??= new EmailSettings();
                newSettings.Notification.Email.SmtpPassword ??= newSettings.Notification.SmtpPassword;
            }
            if (newSettings.Notification?.SmtpUser != null)
            {
                newSettings.Notification.Email ??= new EmailSettings();
                newSettings.Notification.Email.SmtpUser ??= newSettings.Notification.SmtpUser;
            }
            if (newSettings.Notification?.SmtpPort != null)
            {
                newSettings.Notification.Email ??= new EmailSettings();
                newSettings.Notification.Email.SmtpPort ??= newSettings.Notification.SmtpPort;
            }
            if (newSettings.Notification?.SmtpSecure != null)
            {
                newSettings.Notification.Email ??= new EmailSettings();
                newSettings.Notification.Email.SmtpSecure ??= newSettings.Notification.SmtpSecure;
            }
            if (newSettings.Notification?.SmtpSecureMode != null)
            {
                newSettings.Notification.Email ??= new EmailSettings();
                newSettings.Notification.Email.SmtpSecureMode ??= newSettings.Notification.SmtpSecureMode;
            }
            if (newSettings.Notification?.SmtpServer != null)
            {
                newSettings.Notification.Email ??= new EmailSettings();
                newSettings.Notification.Email.SmtpServer ??= newSettings.Notification.SmtpServer;
            }


            newSettings.Validation ??= new ValidationSettings();
            newSettings.Validation.MultiThreading = new MultiThreadingSettings();
            newSettings.Validation.Dns ??= new DnsSettings();
            newSettings.Validation.Dns.Substitution ??= new SubstitutionSettings();
            newSettings.Validation.Dns.PreValidation ??= new PreValidationSettings();
            newSettings.Validation.Http ??= new HttpSettings();
            newSettings.Validation.Http.Ftp ??= new FtpSettings();
            newSettings.Validation.Http.SelfHosting ??= new HttpSelfHostingSettings();
            newSettings.Validation.Tls ??= new TlsSettings();
            newSettings.Validation.Tls.SelfHosting ??= new TlsSelfHostingSettings();

            if (newSettings.Validation?.AllowDnsSubstitution != null)
            {
                newSettings.Validation.Dns.Substitution.Enabled ??= newSettings.Validation.AllowDnsSubstitution;
            }
            if (newSettings.Validation?.CleanupFolders != null)
            {
                newSettings.Validation.Http.CleanupFolders ??= newSettings.Validation.CleanupFolders;
            }
            if (newSettings.Validation?.DisableMultiThreading != null)
            {
                newSettings.Validation.MultiThreading.Enabled ??= !newSettings.Validation.DisableMultiThreading;
            }
            if (newSettings.Validation?.DnsPropagationDelay != null)
            {
                newSettings.Validation.Dns.PreValidation.PropagationDelay ??= newSettings.Validation.DnsPropagationDelay;
            }
            if (newSettings.Validation?.DnsServers != null)
            {
                newSettings.Validation.Dns.Servers ??= newSettings.Validation.DnsServers;
            }
            if (newSettings.Validation?.Ftp != null)
            {
                newSettings.Validation.Http.Ftp ??= newSettings.Validation.Ftp;
            }
            if (newSettings.Validation?.ParallelBatchSize != null)
            {
                newSettings.Validation.MultiThreading.BatchSize ??= newSettings.Validation.ParallelBatchSize;
            }
            if (newSettings.Validation?.PreValidateDns != null)
            {
                newSettings.Validation.Dns.PreValidation.Enabled ??= newSettings.Validation.PreValidateDns;
            }
            if (newSettings.Validation?.PreValidateDnsLocal != null)
            {
                newSettings.Validation.Dns.PreValidation.CheckLocal ??= newSettings.Validation.PreValidateDnsLocal;
            }
            if (newSettings.Validation?.PreValidateDnsRetryCount != null)
            {
                newSettings.Validation.Dns.PreValidation.RetryCount ??= newSettings.Validation.PreValidateDnsRetryCount;
            }
            if (newSettings.Validation?.PreValidateDnsRetryInterval != null)
            {
                newSettings.Validation.Dns.PreValidation.RetryInterval ??= newSettings.Validation.PreValidateDnsRetryInterval;
            }
            return newSettings;
        }
    }

}