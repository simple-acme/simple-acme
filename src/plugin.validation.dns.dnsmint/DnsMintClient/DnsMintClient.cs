using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    /// <summary>
    /// Minimal client for the DNSMint DNS-01 API, which serves the shape
    /// lego's httpreq provider posts: the finished record name and value.
    /// See https://dnsmint.com/api-reference.
    ///
    /// There is no zone to look up and no record id to keep. The hostname is
    /// derived from the record name server-side, and cleanup names the value
    /// to remove, so records the user already had are left alone.
    /// </summary>
    class DnsMintClient
    {
        internal const string Endpoint = "https://dnsmint.com/api/httpreq/";

        private readonly HttpClient _httpClient;

        public DnsMintClient(HttpClient httpClient, string apiKey)
        {
            httpClient.BaseAddress = new Uri(Endpoint);
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
            _httpClient = httpClient;
        }

        internal Task CreateTxtRecord(string recordName, string value) =>
            Post("present", recordName, value, "create TXT record");

        internal Task DeleteTxtRecord(string recordName, string value) =>
            Post("cleanup", recordName, value, "delete TXT record");

        private async Task Post(string action, string recordName, string value, string log)
        {
            var body = new { fqdn = recordName.EndsWith('.') ? recordName : recordName + ".", value };
            using var response = await _httpClient.PostAsJsonAsync(action, body);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync();
                throw new Exception($"Unable to {log}: {response.ReasonPhrase}. {detail}");
            }
        }
    }
}
