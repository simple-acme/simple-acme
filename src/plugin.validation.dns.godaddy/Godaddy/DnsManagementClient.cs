using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Godaddy
{

    public class DnsManagementClient(string apiKey, string apiSecret, ILogService logService, IProxyService proxyService)
    {
        private readonly string uri = "https://api.godaddy.com/";

        public async Task CreateRecord(string domain, string identifier, RecordType type, string value)
        {
            using (var client = proxyService.GetHttpClient())
            {
                client.BaseAddress = new Uri(uri);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrWhiteSpace(apiSecret))
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"sso-key {apiKey}:{apiSecret}");
                } 
                else
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"sso-key {apiKey}");
                }
                var putData = new List<object>() { new { ttl = 600, data = value } };
                var serializedObject = Newtonsoft.Json.JsonConvert.SerializeObject(putData);

                //Record successfully created
                // Wrap our JSON inside a StringContent which then can be used by the HttpClient class
                var typeTxt = type.ToString();
                var httpContent = new StringContent(serializedObject, Encoding.UTF8, "application/json");
                var buildApiUrl = $"v1/domains/{domain}/records/{typeTxt}/{identifier}";
                var response = await client.PutAsync(buildApiUrl, httpContent);
                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent)
                {
                    _ = await response.Content.ReadAsStringAsync();
                }
                else
                {
                    var content = await response.Content.ReadAsStringAsync();
                    throw new Exception(content);
                }


            };
        }

        public async Task DeleteRecord(string domain, string identifier, RecordType type)
        {
            using (var client = proxyService.GetHttpClient())
            {
                client.BaseAddress = new Uri(uri);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                if (!string.IsNullOrWhiteSpace(apiSecret))
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"sso-key {apiKey}:{apiSecret}");
                }
                else
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"sso-key {apiKey}");
                }
                var typeTxt = type.ToString();
                var buildApiUrl = $"v1/domains/{domain}/records/{typeTxt}/{identifier}";

                logService.Information("Godaddy API with: {0}", buildApiUrl);

                var response = await client.DeleteAsync(buildApiUrl);
                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    //_logService.Information("Godaddy Delete Responded with: {0}", content);
                    //_logService.Information("Waiting for 30 seconds");
                    //await Task.Delay(30000);
                }
                else
                {
                    var content = await response.Content.ReadAsStringAsync();
                    throw new Exception(content);
                }
            };
        }
    }
}