using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    class BunnyClient
    {
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Bunny.net API endpoint, as per https://docs.bunny.net/api-reference/core.
        /// </summary>
        private readonly string _bunnyEndpoint = "https://api.bunny.net/";

        /// <summary>
        /// Create a HttpClient with the Bunny.net API endpoint and the API key in the header, as per https://docs.bunny.net/api-reference/authentication.
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="APIKey"></param>
        public BunnyClient(HttpClient httpClient, string APIKey)
        {
            httpClient.BaseAddress = new Uri(_bunnyEndpoint);
            httpClient.DefaultRequestHeaders.Add("AccessKey", APIKey);
            _httpClient = httpClient;
        }

        /// <summary>
        /// Get Zone by domain name, as per https://docs.bunny.net/api-reference/core/dns-zone/list-dns-zones.
        /// Additionally get the Records for the zone, this will be used for delete validation.
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        internal async Task<BunnyZone> GetZone(string domain)
        {
            var response = await GetRequest<BunnyZoneList>($"dnszone?page=1&perPage=1000&search={WebUtility.UrlEncode(domain.ToLowerInvariant())}", "retrieve zone list");
            var item = response?.Items?.Find(i => string.Equals(i.Domain, domain.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase));
            return item == null
                ? throw new Exception($"Unable to retrieve zone id for {domain}")
                : new BunnyZone { Id = item.Id, Domain = item.Domain, Records = item.Records };
        }

        /// <summary>
        /// Common handler for GET requests to the Bunny API
        /// Note that the custom HttpClient already handles 
        /// request and response logging, so we don't need to do 
        /// that here, we only catch errors and throw exceptions if
        /// needed.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task<T> GetRequest<T>(string url, string log)
        {
            using var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Unable to {log}: {response.ReasonPhrase}");
            }
            await using var stream = await response.Content.ReadAsStreamAsync();
            var zones = await JsonSerializer.DeserializeAsync<T>(stream);
            return zones ?? throw new Exception($"Unable to {log}");
        }

        /// <summary>
        /// Create a TXT record in the specified zone with the validation value, as per https://docs.bunny.net/api-reference/core/dns-zone/add-dns-record
        /// </summary>
        /// <param name="zone"></param>
        /// <param name="host"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal async Task CreateTxtRecord(BunnyZone zone, string host, string value)
        {
            var json = JsonSerializer.Serialize(new
            {
                Type = 3, /// TXT
                Name = host,
                Value = value,
                TTL = 60
            });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"dnszone/{zone.Id}/records";
            using var response = await _httpClient.PutAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Unable to create TXT record: {response.ReasonPhrase}");
            }
            return;
        }

        /// <summary>
        /// Check Zone Record List if the TXT record with the specified host and value exists
        /// than delete it, as per https://docs.bunny.net/api-reference/core/dns-zone/delete-dns-record
        /// </summary>
        /// <param name="zone"></param>
        /// <param name="host"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal async Task DeleteTxtRecord(BunnyZone zone, string host, string value)
        {
            var record = zone.Records.Find(r => r.Type == 3 && r.Name == host && r.Value == value) ?? throw new Exception($"Unable to find exact record for deletion");
            var url = $"dnszone/{zone.Id}/records/{record.Id}";
            using var response = await _httpClient.DeleteAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Unable to delete TXT record: {response.ReasonPhrase}");
            }
            return;
        }
    }
}
