using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PKISharp.WACS.Clients
{
    /// <summary>
    /// Sends webhook requests. The URL and headers may contain
    /// secrets, so only the host is ever logged.
    /// </summary>
    internal class WebhookClient(ILogService log, IProxyService proxy)
    {
        internal static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)];
        internal static readonly TimeSpan MaxRetryAfter = TimeSpan.FromSeconds(30);

        internal TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);
        internal Func<TimeSpan, Task> Delay { get; set; } = x => Task.Delay(x);

        /// <summary>
        /// POST the body to the URL, retrying on transient errors
        /// </summary>
        /// <returns>True when the receiver accepted the request</returns>
        public async Task<bool> Send(Uri uri, string body, IEnumerable<KeyValuePair<string, string>> headers)
        {
            using var client = await proxy.GetHttpClient();
            for (var attempt = 0; ; attempt++)
            {
                var problem = "";
                var retryAfter = default(TimeSpan?);
                using var request = new HttpRequestMessage(HttpMethod.Post, uri)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                request.Options.Set(RequestLogger.Confidential, true);
                foreach (var header in headers)
                {
                    if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
                    {
                        // Content headers like Content-Type
                        request.Content.Headers.Remove(header.Key);
                        request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }
                using var timeout = new CancellationTokenSource(Timeout);
                try
                {
                    using var response = await client.SendAsync(request, timeout.Token);
                    if (response.RequestMessage != null && response.RequestMessage.Method != HttpMethod.Post)
                    {
                        // A redirect changed POST to GET, so the body was not delivered
                        log.Error("Webhook notification to {host} was redirected and not delivered, please update the URL", uri.Host);
                        return false;
                    }
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                    problem = $"status {(int)response.StatusCode}";
                    if (!IsTransient(response.StatusCode))
                    {
                        log.Error("Webhook notification to {host} failed with {problem}", uri.Host, problem);
                        return false;
                    }
                    retryAfter = response.Headers.RetryAfter?.Delta ??
                        response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow;
                }
                catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                {
                    problem = "timeout";
                }
                catch (HttpRequestException ex)
                {
                    // The message may contain text from the server
                    problem = $"network error {ex.HttpRequestError}";
                }
                if (attempt >= RetryDelays.Length)
                {
                    log.Error("Webhook notification to {host} failed with {problem}", uri.Host, problem);
                    return false;
                }
                var delay = RetryDelays[attempt];
                if (retryAfter > delay)
                {
                    delay = retryAfter.Value > MaxRetryAfter ? MaxRetryAfter : retryAfter.Value;
                }
                log.Warning("Webhook notification to {host} failed with {problem}, retrying in {seconds} seconds", uri.Host, problem, delay.TotalSeconds);
                await Delay(delay);
            }
        }

        private static bool IsTransient(HttpStatusCode status) =>
            status == HttpStatusCode.TooManyRequests || (int)status >= 500;
    }
}
