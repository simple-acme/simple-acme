using PKISharp.WACS.Plugins.ValidationPlugins.Dns.Models;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Threading.Tasks;

[assembly: SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "<Pending>")]

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns;

public sealed class HetznerClient : IDisposable
{
    private static readonly Uri BASE_ADDRESS = new("https://dns.hetzner.com/api/v1/");

    private readonly ILogService _log;

    private readonly HttpClient _httpClient;

    public HetznerClient(HttpClient client, string apiToken, ILogService logService)
    {
        _log = logService;
        client.BaseAddress = BASE_ADDRESS;
        client.DefaultRequestHeaders.Add("Auth-API-Token", apiToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
        _httpClient = client;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    public async Task<ICollection<Zone>> GetAllZonesAsync()
    {
        var zonesResponse = await _httpClient.GetFromJsonAsync<ZonesResponse>("zones").ConfigureAwait(false);
        if (zonesResponse is null)
        {
            _log.Warning("No zones found in Hetzner DNS");
            return [];
        }

        // Is only one page returned?
        if (zonesResponse.Meta.Pagination.LastPage == zonesResponse.Meta.Pagination.Page)
        {
            return zonesResponse.Zones;
        }

        var allZones = new List<Zone>();
        allZones.AddRange(zonesResponse.Zones);

        // As long as we are not on the last page continue
        while (zonesResponse.Meta.Pagination.LastPage != zonesResponse.Meta.Pagination.Page)
        {
            var nextPage = zonesResponse.Meta.Pagination.Page + 1;

            zonesResponse = await _httpClient.GetFromJsonAsync<ZonesResponse>($"zones?page={nextPage}").ConfigureAwait(false);
            if (zonesResponse is null)
            {
                _log.Warning($"No zones found on page {nextPage} in Hetzner DNS");
                return allZones;
            }

            allZones.AddRange(zonesResponse.Zones);
        }

        return allZones;
    }

    public async Task<Zone?> GetZoneAsync(string zoneId)
    {
        var zoneResponse = await _httpClient.GetFromJsonAsync<Zone>($"zones/{zoneId}").ConfigureAwait(false);
        if (zoneResponse is null)
        {
            _log.Warning($"Zone with zone id {zoneId} not found in Hetzner DNS");
            return null;
        }

        return zoneResponse;
    }

    public async Task<bool> CreateRecordAsync(Record record)
    {
        using var response = await _httpClient.PostAsJsonAsync("records", record).ConfigureAwait(false);

        return response.IsSuccessStatusCode;
    }

    public async Task DeleteRecordAsync(Record record)
    {
        var recordSet = await _httpClient.GetFromJsonAsync<RecordResultSet>($"records?zone_id={record.zone_id}");
        if (recordSet is null || recordSet.records is null || recordSet.records.Length == 0)
        {
            _log.Warning($"Record set for zone id {record.zone_id} is empty");
            return;
        }

        var dnsRecord = recordSet.records
            .Where(x => x.zone_id == record.zone_id && x.name == record.name && x.value == record.value)
            .FirstOrDefault();

        if (dnsRecord is null)
        {
            _log.Warning($"DNS TXT record in zone {record.zone_id} not found");
            return;
        }

        using var deleteResponse = await _httpClient.DeleteAsync($"records/{dnsRecord.id}");

        deleteResponse.EnsureSuccessStatusCode();
    }

    private record RecordResultSet(RecordResult[] records);

    private record RecordResult(string type, string id, string created, string modified, string zone_id, string name, string value, int ttl);
}