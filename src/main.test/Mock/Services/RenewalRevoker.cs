using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.UnitTests.Mock.Services
{
    internal class MockRenewalRevoker(IRenewalStore renewalStore) : IRenewalRevoker
    {
        public Task CancelRenewals(IEnumerable<Renewal> renewals)
        {
            foreach (var renewal in renewals) {
                renewalStore.Cancel(renewal);
            }
            return Task.CompletedTask;
        }

        public Task RevokeCertificates(IEnumerable<Renewal> renewals)
        {
            throw new System.NotImplementedException();
        }
    }
}
