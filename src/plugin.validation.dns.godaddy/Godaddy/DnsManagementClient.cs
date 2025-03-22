using DnsClient.Internal;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Godaddy
{

    public class DnsManagementClient
    {
        private readonly HttpClient _client;

        public DnsManagementClient(string apiKey, string apiSecret, HttpClient client)
        {
            client.BaseAddress = new Uri("https://api.godaddy.com/");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrWhiteSpace(apiSecret))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"sso-key {apiKey}:{apiSecret}");
            }
            else
            {
                client.DefaultRequestHeaders.Add("Authorization", $"sso-key {apiKey}");
            }
            _client = client;
        }

        public async Task CreateRecord(string domain, string identifier, RecordType type, string value)
        {
            var putData = new List<object>() { new { ttl = 600, data = value } };
            var serializedObject = Newtonsoft.Json.JsonConvert.SerializeObject(putData);
            var typeTxt = type.ToString();
            var httpContent = new StringContent(serializedObject, Encoding.UTF8, "application/json");
            var buildApiUrl = $"v1/domains/{domain}/records/{typeTxt}/{identifier}";
            var response = await _client.PutAsync(buildApiUrl, httpContent);
            if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent)
            {
                _ = await response.Content.ReadAsStringAsync();
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new Exception(content);
            }

        }

        public async Task DeleteRecord(string domain, string identifier, RecordType type)
        {
            var typeTxt = type.ToString();
            var buildApiUrl = $"v1/domains/{domain}/records/{typeTxt}/{identifier}";
            var response = await _client.DeleteAsync(buildApiUrl);
            if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent)
            {
                _ = await response.Content.ReadAsStringAsync();
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new Exception(content);
            }
        }
    }
}