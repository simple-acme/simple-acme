using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

// TODO: REMOVE THESE DIRECTIVES - the final code should be without any warnings
#pragma warning disable CS1998
#pragma warning disable CA1822
#pragma warning disable IDE0060

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    class ReferenceClient
    {
        private readonly HttpClient _httpClient;

        /// <summary>
        /// TODO: handle authentication 
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="clientId"></param>
        /// <param name="clientSecret"></param>
        public ReferenceClient(HttpClient httpClient, string clientId, string clientSecret)
        {
            httpClient.DefaultRequestHeaders.Add("ClientId", clientId);
            httpClient.DefaultRequestHeaders.Add("ClientSecret", clientSecret);
            _httpClient = httpClient;
        }

        /// <summary>
        /// TODO: Use our client to get a list of zones
        /// Notes:
        /// - 1. We should use async to avoid blocking the main thread
        /// - 2. Sometimes it will be required to retrieve multiple pages 
        ///      of results to get access to all zone
        /// - 3. When deserialising manually prefer System.Text.Json instead 
        ///      of Newtonsoft.Json
        /// </summary>
        /// <returns></returns>
        internal async Task<IEnumerable<ReferenceZone>> GetZones()
        {
            using var response = await _httpClient.GetAsync("https://example.com/api/dns/zones");
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Unable to retrieve zones: {response.ReasonPhrase}");
            }
            await using var stream = await response.Content.ReadAsStreamAsync();
            var zones = await JsonSerializer.DeserializeAsync<List<ReferenceZone>>(stream);
            return zones ?? throw new Exception("Unable to retrieve zones");
        }

        /// <summary>
        /// TODO: implement this method, be careful not to overwrite existing TXT records
        /// </summary>
        /// <param name="zone"></param>
        /// <param name="host"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal async Task CreateTxtRecord(ReferenceZone zone, string host, string value)
        {
            return;
        }

        /// <summary>
        /// TODO: implement this method, be careful not to delete other pre-existing 
        /// TXT records that the user may have had already before the plugin was used.
        /// The "value" parameter points exactly to the value that should be removed.
        /// </summary>
        /// <param name="zone"></param>
        /// <param name="host"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal async Task DeleteTxtRecord(ReferenceZone zone, string host, string value)
        {
            return;
        }
    }
}
