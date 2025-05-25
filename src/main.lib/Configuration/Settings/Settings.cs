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
        public IEnumerable<TData?> Chain { get; } = chain;
        public TResult? Get<TResult>(Func<TData, TResult> x) => Chain.OfType<TData>().Select(x).FirstOrDefault(v => v != null) ?? default;
    }

    internal class InheritSettings(params IEnumerable<Settings> settings) : InheritSettings<Settings>(settings)
    {
        private readonly IEnumerable<Settings> _settings = settings;
        internal IUiSettings UI => new InheritUiSettings(_settings.Select(c => c.UI));
        internal IClientSettings Client => new InheritClientSettings(_settings.Select(c => c.Client));
        internal IAcmeSettings Acme => new InheritAcmeSettings(_settings.Select(c => c.Acme));
        internal ICacheSettings Cache => new InheritCacheSettings(_settings.Select(c => c.Cache));
        internal IStoreSettings Store => new InheritStoreSettings(_settings.Select(c => c.Store));
        internal ICsrSettings Csr => new InheritCsrSettings(_settings.Select(c => c.Csr));
        internal ISecuritySettings Security => new InheritSecuritySettings(_settings.Select(c => c.Security));
        internal IOrderSettings Order => new InheritOrderSettings(_settings.Select(c => c.Order));
        internal IScriptSettings Script => new InheritScriptSettings(_settings.Select(c => c.Script));
        internal IInstallationSettings Installation => new InheritInstallationSettings(_settings.Select(c => c.Installation));
        internal IExecutionSettings Execution => new InheritExecutionSettings(_settings.Select(c => c.Execution));
        internal ISourceSettings Source => new InheritSourceSettings(_settings.Select(c => c.Source));
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

        ICacheSettings ISettings.Cache => _internal.Cache;
        IAcmeSettings ISettings.Acme => _internal.Acme;
        IUiSettings ISettings.UI => _internal.UI;
        ICsrSettings ISettings.Csr => _internal.Csr;
        IStoreSettings ISettings.Store => _internal.Store;
        IClientSettings ISettings.Client => _internal.Client;
        ISecuritySettings ISettings.Security => _internal.Security;
        IOrderSettings ISettings.Order => _internal.Order;
        IInstallationSettings ISettings.Installation => _internal.Installation;
        IExecutionSettings ISettings.Execution => _internal.Execution;
        IScriptSettings ISettings.Script => _internal.Script;
        ISourceSettings ISettings.Source => _internal.Source;

        IProxySettings ISettings.Proxy => Proxy;
        ISecretsSettings ISettings.Secrets => Secrets;
        IScheduledTaskSettings ISettings.ScheduledTask => ScheduledTask;
        INotificationSettings ISettings.Notification => Notification;
        IValidationSettings ISettings.Validation => Validation;
    }
}