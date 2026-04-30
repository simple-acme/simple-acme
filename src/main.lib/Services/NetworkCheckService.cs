using PKISharp.WACS.Clients.Acme;
using System;
using System.Net.Http;
using System.Security.Authentication;
using System.Text.Json;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    /// <summary>
    /// Used for initial check of network connectivity, to provide early feedback to the 
    /// user if there are issues with the connection to the ACME server. Some attempts
    /// are made to restore connection if it fails, for example by bypassing the proxy 
    /// or forcing TLS 1.2. 
    /// </summary>
    /// <param name="proxy"></param>
    /// <param name="settings"></param>
    /// <param name="log"></param>
    /// <param name="acmeClientManager"></param>
    internal class NetworkCheckService(IProxyService proxy, ISettings settings, ILogService log, AcmeClientManager acmeClientManager)
    {
        /// <summary>
        /// Called from the Banner
        /// </summary>
        /// <returns></returns>
        internal async Task ConnectionTest()
        {
            // Connection test
            log.Information("Connecting to {ACME}...", settings.BaseUri);
            var success = await CheckNetworkWithTimeout();
            if (!success)
            {
                log.Debug("Connection failed, retrying with TLS 1.2 forced...");
                proxy.SslProtocols = SslProtocols.Tls12;
                success = await CheckNetworkWithTimeout();
            } 
            if (!success)
            {
                log.Debug("Connection failed, retrying with proxy bypass...");
                proxy.Disable();
                success = await CheckNetworkWithTimeout();
            }
            if (!success)
            {
                log.Warning("Network check failed or timed out. Functionality may be limited.");
                return;
            }
            log.Information("Connection OK!");
        }

        /// <summary>
        /// If any connection test takes too long, we also consider it failed
        /// </summary>
        /// <returns></returns>
        private async Task<bool> CheckNetworkWithTimeout()
        {
            try
            {
                var result = CheckNetwork();
                return await result.WaitAsync(TimeSpan.FromSeconds(15));
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        /// <summary>
        /// Test the network connection using candidate directory URLs from the AcmeClientManager
        /// </summary>
        private async Task<bool> CheckNetwork()
        {
            using var httpClient = await proxy.GetHttpClient();
            httpClient.BaseAddress = settings.BaseUri;
            httpClient.Timeout = new TimeSpan(0, 0, 10);
            foreach (var url in acmeClientManager.GetDirectorUrls())
            {
                if (await CheckNetworkUrl(httpClient, url))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Try to connect to the given URL and check if it returns a valid response.
        /// </summary>
        private async Task<bool> CheckNetworkUrl(HttpClient httpClient, string path)
        {
            try
            {
                var response = await httpClient.GetAsync(path).ConfigureAwait(false);
                await CheckNetworkResponse(response);
                return true;
            }
            catch (HttpRequestException ex)
            {
                log.Debug($"{{error}}: {ex.Message} ({{status}})", ex.HttpRequestError, (int?)ex.StatusCode ?? -1);
                return false;
            }
            catch (Exception ex)
            {
                log.Debug($"Connection failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deep inspection of initial response
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async static Task CheckNetworkResponse(HttpResponseMessage response)
        {
            if (response == null)
            {
                throw new Exception($"Server returned emtpy response");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Server returned status {(int)response.StatusCode}: {response.ReasonPhrase ?? response.StatusCode.ToString()}");
            }
            string? content;
            try
            {
                content = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to get response content", ex);
            }
            try
            {
                JsonSerializer.Deserialize(content, AcmeClientJson.Insensitive.ServiceDirectory);
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to parse response content", ex);
            }
        }
    }
}
