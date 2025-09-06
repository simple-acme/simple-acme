using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins;

class WebnamesClient
{
    private readonly HttpClient _httpClient;
    public static readonly String DefaultBaseURL = "https://www.webnames.ca/_/APICore";

    protected String _baseURL = DefaultBaseURL;

    /// <summary>
    /// Initialize a new HTTP client for the Webnames REST API.
    /// See https://www.webnames.ca/_/swagger/index.html for API documentation.
    /// </summary>
    /// <param name="httpClient">.NET HttpClient instance.</param>
    /// <param name="APIUsername">API Username.</param>
    /// <param name="APIKey">API Key.</param>
    public WebnamesClient(HttpClient httpClient, string APIUsername, string APIKey, string? baseURL = null)
    {
        httpClient.DefaultRequestHeaders.Add("API-User", APIUsername);
        httpClient.DefaultRequestHeaders.Add("API-Key", APIKey);
        _httpClient = httpClient;
        _baseURL = baseURL ?? DefaultBaseURL;
    }

    /// <summary>
    /// Adds a single TXT record to the specified hostName on the specified domain's DNS zone. 
    /// No records will be removed or replaced. If an identical TXT record is already present, 
    /// it will be retained.
    /// </summary>
    /// <param name="domainName">Root registerable domain name (zone) to update.</param>
    /// <param name="host">Hostname of TXT record to add.</param>
    /// <param name="value">Value of TXT record to add.</param>
    internal async Task CreateTxtRecord(string domainName, string host, string value)
    {
        domainName = System.Net.WebUtility.UrlEncode(domainName);
        host = System.Net.WebUtility.UrlEncode(host);
        value = System.Net.WebUtility.UrlEncode(value);

        await _httpClient.PostAsync($"{_baseURL}/domains/{domainName}/add-txt-record?hostName={host}&txt={value}", null);
    }

    /// <summary>
    /// Deletes the specified TXT record from the specified hostName on the specified domain's 
    /// DNS zone. If no matching TXT record is present, no action is taken.
    /// </summary>
    /// <param name="domainName">Root registerable domain name (zone) to update.</param>
    /// <param name="host">Hostname of TXT record to delete.</param>
    /// <param name="value">Value of TXT record to delete.</param>
    internal async Task DeleteTxtRecord(string domainName, string host, string value)
    {
        domainName = System.Net.WebUtility.UrlEncode(domainName);
        host = System.Net.WebUtility.UrlEncode(host);
        value = System.Net.WebUtility.UrlEncode(value);

        await _httpClient.DeleteAsync($"{_baseURL}/domains/{domainName}/delete-txt-record?hostName={host}&txt={value}");
    }
}
