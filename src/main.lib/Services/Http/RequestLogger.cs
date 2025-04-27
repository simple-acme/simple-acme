using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    internal class RequestLogger(ILogService log)
    {
        /// <summary>
        /// Common pre-send functionality
        /// </summary>
        /// <param name="request"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task PreSend(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            log.Debug("[HTTP] Send {method} to {uri}", request.Method, request.RequestUri);
            if (request.Content != null)
            {
                var content = await request.Content.ReadAsStringAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    log.Verbose("[HTTP] Request content: {content}", content);
                }
            }
        }

        /// <summary>
        /// Common post-send functionality
        /// </summary>
        /// <param name="response"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task PostSend(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            if (response.IsSuccessStatusCode)
            {
                log.Verbose("[HTTP] Request completed with status {s}", response.StatusCode);
            }
            else
            {
                log.Warning("[HTTP] Request completed with status {s}", response.StatusCode);
            }
            if (response.Content != null)
            {
                var printableTypes = new[] {
                    "text/json",
                    "application/json",
                    "application/problem+json"
                };
                var responseType = response.Content.Headers.ContentType?.MediaType ?? "unknown";
                if (printableTypes.Contains(responseType))
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        log.Verbose("[HTTP] Response content: {content}", content);
                    }
                    else
                    {
                        log.Verbose("[HTTP] Empty response of type {type}", responseType);
                    }
                }
                else
                {
                    log.Verbose("[HTTP] Response of type {type} ({bytes} bytes)", responseType, response.Content.Headers.ContentLength?.ToString() ?? "?");
                }
            }
            else
            {
                log.Verbose("[HTTP] Empty response");
            }
        }
    }
}