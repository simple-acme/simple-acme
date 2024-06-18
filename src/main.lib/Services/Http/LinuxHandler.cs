using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    internal class LinuxHandler : HttpClientHandler
    {
        private readonly RequestLogger _log;

        public LinuxHandler(RequestLogger log) => _log = log;

        /// <summary>
        /// Asynchronous request
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await _log.PreSend(request, cancellationToken);
            var response = await base.SendAsync(request, cancellationToken);
            await _log.PostSend(response, cancellationToken);
            return response;
        }
    }
}