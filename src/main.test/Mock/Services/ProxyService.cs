using PKISharp.WACS.Services;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace PKISharp.WACS.UnitTests.Mock.Services
{
    internal class ProxyService : IProxyService
    {
        public SslProtocols SslProtocols { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
        public WindowsProxyUsePolicy ProxyType => throw new System.NotImplementedException();
        public void Disable() => throw new System.NotImplementedException();
        public Task<HttpClient> GetHttpClient(bool checkSsl = true) => Task.FromResult(new HttpClient());
        public Task<HttpMessageHandler> GetHttpMessageHandler() => throw new System.NotImplementedException();
        public Task<IWebProxy?> GetWebProxy() => throw new System.NotImplementedException();
    }
}