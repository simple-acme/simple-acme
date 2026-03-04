
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Czechia;

internal sealed class CzechiaClient
{
    private readonly HttpClient _http;
    private readonly string _baseUri;
    private readonly int _ttl;

    public CzechiaClient(HttpClient httpClient, string apiBaseUri, string apiToken, int ttl)
    {
        _http = httpClient;
        _baseUri = apiBaseUri.TrimEnd('/');
        _ttl = ttl;

        _http.DefaultRequestHeaders.Remove("AuthorizationToken");
        _http.DefaultRequestHeaders.Add("AuthorizationToken", apiToken);
    }

    private sealed record TxtBody(string hostName, string text, int ttl, int publishZone);

    public async Task CreateTxtRecord(string zone, string hostName, string text)
    {
        var url = $"{_baseUri}/DNS/{Uri.EscapeDataString(zone)}/TXT";
        var body = new TxtBody(hostName, text, _ttl, 1);

        using var resp = await _http.PostAsJsonAsync(url, body);
        resp.EnsureSuccessStatusCode();
    }

    public async Task DeleteTxtRecord(string zone, string hostName, string text)
    {
        var url = $"{_baseUri}/DNS/{Uri.EscapeDataString(zone)}/TXT";
        var body = new TxtBody(hostName, text, _ttl, 1);

        using var req = new HttpRequestMessage(HttpMethod.Delete, url)
        {
            Content = JsonContent.Create(body)
        };

        using var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();
    }
}
