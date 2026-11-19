using PKISharp.WACS.Plugins.NotificationPlugins;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Interfaces;
using System.Collections.Generic;

namespace PKISharp.WACS.UnitTests.Mock.Services
{
    internal class MockAssemblyService(ILogService log) : AssemblyService(log)
    {
        public override List<TypeDescriptor> GetResolvable<T>()
        {
            // Fake that only a single ISecretProvider exists
            if (typeof(T) == typeof(ISecretProvider))
            {
                return [new(typeof(SecretService))];
            }
            // Test doubles in this assembly would otherwise be discovered as targets
            if (typeof(T) == typeof(INotificationTarget))
            {
                return [new(typeof(NotificationTargetEmail)), new(typeof(NotificationTargetScript)), new(typeof(NotificationTargetWebhook))];
            }
            return base.GetResolvable<T>();

        }
    }
}
