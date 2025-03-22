using PKISharp.WACS.Services;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns.NS1
{
    public class DnsManagementClient 
    {
        private readonly HttpClient _httpClient;

        public DnsManagementClient(string apiKey, HttpClient client)
        {
            client.BaseAddress = new Uri(_uri);
            client.DefaultRequestHeaders.Add("X-NSONE-Key", apiKey);
            _httpClient = client;
        }

        private readonly string _uri = "https://api.nsone.net/v1/";

        public async Task<string[]?> GetZones()
        {
            try
            {
                using var response = await _httpClient.GetAsync("zones");
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return JsonDocument.Parse(response.Content.ReadAsStream())
                    .RootElement.EnumerateArray()
                    .Select(x => x.GetProperty("zone").GetString())
                    .OfType<string>()
                    .ToArray();
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> CreateRecord(string zone, string domain, string type, string value)
        {
            // {"zone":"example.com","domain":"sub.example.com","type":"TXT","ttl":0,"answers":[{"answer":["content"]}]}
            var json = JsonSerializer.SerializeToUtf8Bytes(new {
                zone, domain, type, ttl = 0,
                answers = new object[] { new { answer = new string[] { value } } }
            });

            using var response = await _httpClient.PutAsync($"zones/{zone}/{domain}/{type}", new ByteArrayContent(json));
            var err = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            return true;
        }

        public async Task<bool> DeleteRecord(string zone, string domain, string type)
        {
            using var response = await _httpClient.DeleteAsync($"zones/{zone}/{domain}/{type}");
            return response.IsSuccessStatusCode;
        }
    }
}
