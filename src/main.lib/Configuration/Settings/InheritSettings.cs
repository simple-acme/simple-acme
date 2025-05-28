using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Configuration.Settings
{
    internal class InheritSettings<TData>(params IEnumerable<TData?> chain)
    {
        public IEnumerable<TData?> Chain { get; } = chain;
        public TResult? Get<TResult>(Func<TData, TResult?> x) => Chain.OfType<TData>().Select(x).FirstOrDefault(v => v != null);
    }

    internal class InheritSettings(params IEnumerable<Settings> settings) : InheritSettings<Settings>(settings), ISettings
    {
        public readonly IEnumerable<Settings> Settings = settings;
        public IUiSettings UI => new InheritUiSettings(Settings.Select(c => c.UI));
        public IClientSettings Client => new InheritClientSettings(this, Settings.Select(c => c.Client));
        public IAcmeSettings Acme => new InheritAcmeSettings(Settings.Select(c => c.Acme));
        public ICacheSettings Cache => new InheritCacheSettings(this, Settings.Select(c => c.Cache));
        public IStoreSettings Store => new InheritStoreSettings(Settings.Select(c => c.Store));
        public ICsrSettings Csr => new InheritCsrSettings(Settings.Select(c => c.Csr));
        public ISecuritySettings Security => new InheritSecuritySettings(Settings.Select(c => c.Security));
        public IOrderSettings Order => new InheritOrderSettings(Settings.Select(c => c.Order));
        public IScriptSettings Script => new InheritScriptSettings(Settings.Select(c => c.Script));
        public IInstallationSettings Installation => new InheritInstallationSettings(Settings.Select(c => c.Installation));
        public IExecutionSettings Execution => new InheritExecutionSettings(Settings.Select(c => c.Execution));
        public ISourceSettings Source => new InheritSourceSettings(Settings.Select(c => c.Source));
        public IScheduledTaskSettings ScheduledTask => new InheritScheduledTaskSettings(Settings.Select(c => c.ScheduledTask));
        public IProxySettings Proxy => new InheritProxySettings(Settings.Select(c => c.Proxy));
        public ISecretsSettings Secrets => new InheritSecretsSettings(Settings.Select(c => c.Secrets));
        public INotificationSettings Notification => new InheritNotificationSettings(Settings.Select(c => c.Notification));
        public IValidationSettings Validation => new InheritValidationSettings(Settings.Select(c => c.Validation));
        public bool Valid { get; set; } = false;
        public Uri BaseUri { get; set; } = new Uri("https://localhost");
    }
}
