using PKISharp.WACS.Services;
using System.Collections.Generic;

namespace PKISharp.WACS.UnitTests.Mock.Services
{
    internal class MockAssemblyService(ILogService log) : AssemblyService(log)
    {
        public override List<TypeDescriptor> GetResolvable<T>()
        {
            if (typeof(T) == typeof(ISecretService))
            {
                return [new(typeof(SecretService))];
            }
            return base.GetResolvable<T>();

        }
    }
}
