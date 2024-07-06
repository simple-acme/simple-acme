using PKISharp.WACS.Clients.Acme;
using System;
using System.Net.Http;
using System.Security.Authentication;
using System.Text.Json;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    internal class NetworkCheckService(IProxyService proxy, ISettingsService settings, ILogService log)
    {

        /// <summary>
        /// Test the network connection
        /// </summary>
        internal async Task CheckNetwork()
        {
            using var httpClient = proxy.GetHttpClient();
            httpClient.BaseAddress = settings.BaseUri;
            httpClient.Timeout = new TimeSpan(0, 0, 10);
            var success = await CheckNetworkUrl(httpClient, "directory");
            if (!success)
            {
                success = await CheckNetworkUrl(httpClient, "");
            }
            if (!success)
            {
                log.Debug("Initial connection failed, retrying with TLS 1.2 forced");
                proxy.SslProtocols = SslProtocols.Tls12;
                success = await CheckNetworkUrl(httpClient, "directory");
                if (!success)
                {
                    success = await CheckNetworkUrl(httpClient, "");
                }
            }
            if (success)
            {
                log.Verbose("Connection OK!");
            }
            else
            {
                log.Warning("Initial connection failed");
            }
        }

        /// <summary>
        /// Test the network connection
        /// </summary>
        private async Task<bool> CheckNetworkUrl(HttpClient httpClient, string path)
        {
            try
            {
                var response = await httpClient.GetAsync(path).ConfigureAwait(false);
                await CheckNetworkResponse(response);
                return true;
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
                throw new Exception($"Server returned status {response.StatusCode}:{response.ReasonPhrase}");
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
