using PKISharp.WACS.Services;
using System.Net.Http;
using System.Threading.Tasks;

namespace TransIp.Library
{
    public abstract class BaseServiceAuthenticated(AuthenticationService authenticationService, IProxyService proxyService) : BaseService(proxyService)
    {
        protected internal override async Task<HttpClient> GetClient() => 
            await authenticationService.GetClient();
    }
}
