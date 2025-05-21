using PKISharp.WACS.Services;
using System;
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

    /// <summary>
    /// All settings
    /// </summary>
    public class Settings : ISettings
    {
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
        [Obsolete("Use Source instead")]
        public SourceSettings Target { get; set; } = new SourceSettings();
        public SourceSettings Source { get; set; } = new SourceSettings();
        public ValidationSettings Validation { get; set; } = new ValidationSettings();
        public OrderSettings Order { get; set; } = new OrderSettings();
        public CsrSettings Csr { get; set; } = new CsrSettings();
        public StoreSettings Store { get; set; } = new StoreSettings();
        public InstallationSettings Installation { get; set; } = new InstallationSettings();
        public SecretsSettings Secrets { get; set; } = new SecretsSettings();
        public bool Valid { get; set; } = false;
    }
}