using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    /// <summary>
    /// Minimal client for the Level27 DNS API.
    /// See https://api.level27.eu/v1 and the official Go client
    /// (https://github.com/level27/l27-go) for the API shapes used here.
    /// </summary>
    class Level27Client
    {
        /// <summary>
        /// Default Level27 API endpoint.
        /// </summary>
        internal const string DefaultEndpoint = "https://api.level27.eu/v1";

        private readonly HttpClient _httpClient;

        /// <summary>
        /// Create a client for the Level27 API. Authentication is done by
        /// sending the API key in the Authorization header, as implemented in
        /// the official acme.sh integration written by Level27.
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="apiKey"></param>
        /// <param name="apiBaseUrl"></param>
        public Level27Client(HttpClient httpClient, string apiKey, string? apiBaseUrl)
        {
            var endpoint = string.IsNullOrWhiteSpace(apiBaseUrl) ? DefaultEndpoint : apiBaseUrl.TrimEnd('/');
            httpClient.BaseAddress = new Uri(endpoint + "/");
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", apiKey);
            _httpClient = httpClient;
        }

        /// <summary>
        /// Look up the zones that match the given domain name. The Level27 API
        /// filter is a substring search, so this can return the registered
        /// domain as well as any delegated subdomains.
        /// </summary>
        /// <param name="domain"></param>
        /// <returns></returns>
        internal async Task<IEnumerable<Level27Zone>> GetZones(string domain)
        {
            var response = await GetRequest<Level27DomainList>($"domains?filter={WebUtility.UrlEncode(domain)}", "retrieve zone list");
            return response.Domains.Select(d => new Level27Zone { Id = d.Id, Name = d.Fullname });
        }

        /// <summary>
        /// Create a TXT record in the specified zone. Existing records are left
        /// untouched because the Level27 API adds a new record for each POST.
        /// </summary>
        /// <param name="zone"></param>
        /// <param name="host"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal async Task CreateTxtRecord(Level27Zone zone, string host, string value)
        {
            var body = new { name = host, type = "TXT", content = value };
            using var response = await _httpClient.PostAsJsonAsync($"domains/{zone.Id}/records", body);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Unable to create TXT record: {response.ReasonPhrase}");
            }
        }

        /// <summary>
        /// Delete the TXT record with the exact host and value from the zone,
        /// so that any pre-existing TXT records the user may have are preserved.
        /// </summary>
        /// <param name="zone"></param>
        /// <param name="host"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal async Task DeleteTxtRecord(Level27Zone zone, string host, string value)
        {
            var records = await GetRequest<Level27RecordList>($"domains/{zone.Id}/records?type=TXT", "retrieve TXT records");
            var record = records.Records.FirstOrDefault(r =>
                string.Equals(r.Type, "TXT", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(r.Name, host, StringComparison.OrdinalIgnoreCase) &&
                MatchesValue(r.Content, value));
            if (record == null)
            {
                throw new Exception("Unable to find exact record for deletion");
            }
            using var response = await _httpClient.DeleteAsync($"domains/{zone.Id}/records/{record.Id}");
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Unable to delete TXT record: {response.ReasonPhrase}");
            }
        }

        /// <summary>
        /// The Level27 API may store TXT content with or without surrounding
        /// quotes, so accept both representations when matching.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        private static bool MatchesValue(string content, string value) =>
            string.Equals(content, value, StringComparison.Ordinal) ||
            string.Equals(content, $"\"{value}\"", StringComparison.Ordinal);

        /// <summary>
        /// Common handler for GET requests to the Level27 API. Note that the
        /// custom HttpClient already handles request and response logging, so we
        /// only catch errors and throw exceptions if needed.
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
            var result = await JsonSerializer.DeserializeAsync<T>(stream);
            return result ?? throw new Exception($"Unable to {log}");
        }
    }
}
