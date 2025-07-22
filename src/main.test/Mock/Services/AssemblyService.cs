using PKISharp.WACS.Services;
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
            return base.GetResolvable<T>();

        }
    }
}
