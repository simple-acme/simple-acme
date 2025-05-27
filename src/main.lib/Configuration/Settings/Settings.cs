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
        public TResult? Get<TResult>(Func<TData, TResult?> x) => Chain.OfType<TData>().Select(x).FirstOrDefault(v => v != null);
    }

    internal class InheritSettings(params IEnumerable<Settings> settings) : InheritSettings<Settings>(settings), ISettings
    {
        private readonly IEnumerable<Settings> _settings = settings;
        public IUiSettings UI => new InheritUiSettings(_settings.Select(c => c.UI));
        public IClientSettings Client => new InheritClientSettings(this, _settings.Select(c => c.Client));
        public IAcmeSettings Acme => new InheritAcmeSettings(_settings.Select(c => c.Acme));
        public ICacheSettings Cache => new InheritCacheSettings(this, _settings.Select(c => c.Cache));
        public IStoreSettings Store => new InheritStoreSettings(_settings.Select(c => c.Store));
        public ICsrSettings Csr => new InheritCsrSettings(_settings.Select(c => c.Csr));
        public ISecuritySettings Security => new InheritSecuritySettings(_settings.Select(c => c.Security));
        public IOrderSettings Order => new InheritOrderSettings(_settings.Select(c => c.Order));
        public IScriptSettings Script => new InheritScriptSettings(_settings.Select(c => c.Script));
        public IInstallationSettings Installation => new InheritInstallationSettings(_settings.Select(c => c.Installation));
        public IExecutionSettings Execution => new InheritExecutionSettings(_settings.Select(c => c.Execution));
        public ISourceSettings Source => new InheritSourceSettings(_settings.Select(c => c.Source));
        public IScheduledTaskSettings ScheduledTask => new InheritScheduledTaskSettings(_settings.Select(c => c.ScheduledTask));
        public IProxySettings Proxy => new InheritProxySettings(_settings.Select(c => c.Proxy));
        public ISecretsSettings Secrets => new InheritSecretsSettings(_settings.Select(c => c.Secrets));
        public INotificationSettings Notification => new InheritNotificationSettings(_settings.Select(c => c.Notification));
        public IValidationSettings Validation => new InheritValidationSettings(_settings.Select(c => c.Validation));
        public bool Valid { get; set; } = false;
        public Uri BaseUri { get; set; } = new Uri("https://localhost");
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
    }
}