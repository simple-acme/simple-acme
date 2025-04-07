using PKISharp.WACS.DomainObjects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    internal interface IRenewalStoreBackend
    {
        Task<IEnumerable<Renewal>> Read();
        Task Write(IEnumerable<Renewal> renewals);
    }

    internal interface IRenewalStore
    {
        Task<IEnumerable<Renewal>> FindByArguments(string? id, string? friendlyName);
        Task Save(Renewal renewal, RenewResult result);
        Task Cancel(Renewal renewal);
        Task Clear();
        Task Import(Renewal renewal);
        Task Encrypt();
        Task<List<Renewal>> List();
    }
}
