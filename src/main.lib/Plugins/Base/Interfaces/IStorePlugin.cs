using PKISharp.WACS.DomainObjects;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.Interfaces
{
    public interface IStorePlugin : IPlugin
    {
        /// <summary>
        /// Persist certificate and update CertificateInfo
        /// </summary>
        /// <param name="certificateInfo"></param>
        Task<StoreInfo?> Save(ICertificateInfo certificateInfo, IFriendlyNameInfo nameInfo);

        /// <summary>
        /// Remove certificate from persisted storage
        /// </summary>
        /// <param name="certificateInfo"></param>
        Task Delete(ICertificateInfo certificateInfo, IFriendlyNameInfo nameInfo);
    }
}
