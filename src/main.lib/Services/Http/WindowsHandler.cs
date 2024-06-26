using System.Net.Http;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    [SupportedOSPlatform("windows")]
    internal class WindowsHandler(RequestLogger log) : WinHttpHandler
    {

        /// <summary>
        /// Asynchronous request
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await log.PreSend(request, cancellationToken);
            var response = await base.SendAsync(request, cancellationToken);
            await log.PostSend(response, cancellationToken);
            return response;
        }
    }
}