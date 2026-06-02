using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dnsexit
{

    public class DnsManagementClient
    {
        private readonly string uri = "https://api.dnsexit.com/dns/";
        private readonly string apiKey;
        private readonly HttpClient httpClient;

        public DnsManagementClient(string apiKey, HttpClient httpClient)
        {
            this.apiKey = apiKey;
            this.httpClient = httpClient;
            httpClient.BaseAddress = new Uri(uri);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task CreateRecord(string domain, string identifier, RecordType type, string value)
        {
           
            var postData = new
            {
                apikey = apiKey,
                domain,
                add = new {
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
            var response = await httpClient.PostAsync("", httpContent);
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
            // BaseAddress and Accept headers are configured in the constructor
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
            var httpContent = new StringContent(serializedObject, Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync("", httpContent);
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