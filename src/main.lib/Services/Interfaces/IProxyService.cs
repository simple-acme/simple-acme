using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    public interface IProxyService
    {
        SslProtocols SslProtocols { get; set; }
        WindowsProxyUsePolicy ProxyType { get; }
        Task<HttpMessageHandler> GetHttpMessageHandler();
        Task<HttpClient> GetHttpClient(bool checkSsl = true);
        Task<IWebProxy?> GetWebProxy();
        void Disable();
    }
}
