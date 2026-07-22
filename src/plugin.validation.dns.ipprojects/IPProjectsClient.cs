using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins;

public class IPProjectsClient
{
    private readonly HttpClient httpClient;

    private const string baseUri = "https://api.ip-projects.de/v1/dns/acme/";

    public IPProjectsClient(HttpClient httpClient, string clientSecret)
    {
        httpClient.DefaultRequestHeaders.Add("X-API-Key", clientSecret);
        httpClient.BaseAddress = new Uri(baseUri);
        this.httpClient = httpClient;
    }

    internal async Task<bool> AddRecord(string domain, string key, string value)
    {
        return await PostAction("add", domain, key, value);
    }

    internal async Task<bool> DeleteRecord(string domain, string key, string value)
    {
        return await PostAction("remove", domain, key, value);
    }

    private async Task<bool> PostAction(string action, string domain, string key, string value)
    {
        using HttpResponseMessage response = await httpClient.PostAsJsonAsync(action, new { domain, key, value });
        return response.IsSuccessStatusCode;
    }
}
