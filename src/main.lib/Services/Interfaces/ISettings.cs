using PKISharp.WACS.Configuration.Settings.Types;
using System;

namespace PKISharp.WACS.Services
{
    public interface ISettings
    { 
        IUiSettings UI { get; }
        IAcmeSettings Acme { get; }
        IExecutionSettings Execution { get; }
        IProxySettings Proxy { get; }
        ICacheSettings Cache { get; }
        ISecretsSettings Secrets { get; }
        IScheduledTaskSettings ScheduledTask { get; }
        INotificationSettings Notification { get; }
        ISecuritySettings Security { get; }
        IScriptSettings Script { get; }
        IClientSettings Client { get; }
        ISourceSettings Source { get; }
        IValidationSettings Validation { get; }
        IOrderSettings Order { get; }
        ICsrSettings Csr { get; }
        IStoreSettings Store { get; }
        IInstallationSettings Installation { get; }
        Uri BaseUri { get; }
        bool Valid { get; }
    }
}
