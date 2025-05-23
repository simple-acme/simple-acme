using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Configuration.Settings
{
    [JsonSerializable(typeof(Settings))]
    internal partial class SettingsJson : JsonSerializerContext 
    {
        public static SettingsJson Insensitive => new(new JsonSerializerOptions() { 
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        });
    }

    internal class InheritSettings<TData>(params IEnumerable<TData?> chain)
    {
        public TResult? Get<TResult>(Func<TData, TResult> x) => chain.OfType<TData>().Select(x).FirstOrDefault(v => v != null) ?? default;
    }

    internal class InheritSettings(params IEnumerable<Settings> settings) : InheritSettings<Settings>(settings)
    {
        private readonly IEnumerable<Settings> _settings = settings;
        internal IUiSettings UI => new InheritUiSettings(_settings.Select(c => c.UI));
        internal IClientSettings Client => new InheritClientSettings(_settings.Select(c => c.Client));
        internal IAcmeSettings Acme => new InheritAcmeSettings(_settings.Select(c => c.Acme));
    }

    /// <summary>
    /// All settings
    /// </summary>
    internal class Settings : ISettings
    {
        private readonly InheritSettings _internal;

        public Settings() => _internal = new InheritSettings(this);
        public ClientSettings Client { get; set; } = new ClientSettings();
        public UiSettings UI { get; set; } = new UiSettings();
        public AcmeSettings Acme { get; set; } = new AcmeSettings();
        public ExecutionSettings Execution { get; set; } = new ExecutionSettings();
        public ProxySettings Proxy { get; set; } = new ProxySettings();
        public CacheSettings Cache { get; set; } = new CacheSettings();
        public ScheduledTaskSettings ScheduledTask { get; set; } = new ScheduledTaskSettings();
        public NotificationSettings Notification { get; set; } = new NotificationSettings();
        public SecuritySettings Security { get; set; } = new SecuritySettings();
        public ScriptSettings Script { get; set; } = new ScriptSettings();
        public SourceSettings Target { get; set; } = new SourceSettings();
        public SourceSettings Source { get; set; } = new SourceSettings();
        public ValidationSettings Validation { get; set; } = new ValidationSettings();
        public OrderSettings Order { get; set; } = new OrderSettings();
        public CsrSettings Csr { get; set; } = new CsrSettings();
        public StoreSettings Store { get; set; } = new StoreSettings();
        public InstallationSettings Installation { get; set; } = new InstallationSettings();
        public SecretsSettings Secrets { get; set; } = new SecretsSettings();
        public bool Valid { get; set; } = false;
        public Uri BaseUri { get; set; } = new Uri("https://localhost");

        ICacheSettings ISettings.Cache => Cache;
        IAcmeSettings ISettings.Acme => _internal.Acme;
        IUiSettings ISettings.UI => _internal.UI;
        IExecutionSettings ISettings.Execution => Execution;
        IProxySettings ISettings.Proxy => Proxy;
        ISecretsSettings ISettings.Secrets => Secrets;
        IScheduledTaskSettings ISettings.ScheduledTask => ScheduledTask;
        INotificationSettings ISettings.Notification => Notification;
        ISecuritySettings ISettings.Security => Security;
        IScriptSettings ISettings.Script => Script;
        IClientSettings ISettings.Client => _internal.Client;
        ISourceSettings ISettings.Source => Source;
        IValidationSettings ISettings.Validation => Validation;
        IOrderSettings ISettings.Order => Order;
        ICsrSettings ISettings.Csr => Csr;
        IStoreSettings ISettings.Store => Store;
        IInstallationSettings ISettings.Installation => Installation;
    }
}