using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.UnitTests.Mock.Services
{
    internal class MockRenewalStore : IRenewalStoreBackend
    {
        /// <summary>
        /// Local cache to prevent superfluous reading and
        /// JSON parsing
        /// </summary>
        internal List<Renewal> _renewalsCache;

        public MockRenewalStore()
        {
            _renewalsCache =
            [
                new() { Id = "1" }
            ];
        }

        public Task<IEnumerable<Renewal>> Read()
        {
            return Task.FromResult(_renewalsCache.Where(x => !x.Deleted));
        }

        public Task Write(IEnumerable<Renewal> renewals)
        {
            _renewalsCache = [.. renewals];
            return Task.CompletedTask;
        }
    }
}
