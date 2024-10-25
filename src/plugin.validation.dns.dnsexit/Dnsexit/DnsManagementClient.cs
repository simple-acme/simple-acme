using PKISharp.WACS.Services;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dnsexit
{

    public class DnsManagementClient(string apiKey, ILogService logService, IProxyService proxyService)
    {
        private readonly string uri = "https://api.dnsexit.com/dns/";

        public async Task CreateRecord(string domain, string identifier, RecordType type, string value)
        {
            using (var client = proxyService.GetHttpClient())
            {
                client.BaseAddress = new Uri(uri);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var postData = new
                {
                    apikey = apiKey,
                    domain,
                    add = new
                    {
                        type = type.ToString(),
                        name = identifier,
                        content = value,
                        ttl = 600,
                        overwrite = true
                    }
                };

                var serializedPost = Newtonsoft.Json.JsonConvert.SerializeObject(postData);

                //Record successfully created
                // Wrap our JSON inside a StringContent which then can be used by the HttpClient class
                var httpContent = new StringContent(serializedPost, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("", httpContent);
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

                var postData = new
                {
                    apikey = apiKey,
                    domain,
                    delete = new
                    {
                        type = type.ToString(),
                        name = identifier,
                    }
                };

                var serializedObject = Newtonsoft.Json.JsonConvert.SerializeObject(postData);

                logService.Information($"Deleting {type} record, {identifier} with Dnsexit API...");

                var httpContent = new StringContent(serializedObject, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("", httpContent);
                if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent)
                {
                    var content = await response.Content.ReadAsStringAsync();
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