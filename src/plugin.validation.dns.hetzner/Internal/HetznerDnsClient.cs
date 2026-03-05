using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mime;
using System.Threading.Tasks;

using PKISharp.WACS.Services;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns.Internal.Models;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns.Internal.Models.HetznerDns;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns.Internal;

internal sealed class HetznerDnsClient : IHetznerClient, IDisposable
{
    private static readonly Uri BASE_ADDRESS = new Uri("https://dns.hetzner.com/api/v1/");

    private ILogService _log;

    private HttpClient _httpClient;

    public HetznerDnsClient(string apiToken, ILogService logService, HttpClient httpClient)
    {
        _log = logService;

        _httpClient = httpClient;
        _httpClient.BaseAddress = BASE_ADDRESS;
        _httpClient.DefaultRequestHeaders.Add("Auth-API-Token", apiToken);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    public async Task<IReadOnlyCollection<HetznerZone>> GetAllActiveZonesAsync()
    {
        var zonesResponse = await _httpClient.GetFromJsonAsync<ZonesResponse>("zones").ConfigureAwait(false);
        if (zonesResponse is null)
        {
            _log.Warning("No zones found in Hetzner DNS");
            return Array.Empty<HetznerZone>();
        }

        // Is only one page returned?
        if (zonesResponse.Meta.Pagination.LastPage == zonesResponse.Meta.Pagination.Page)
        {
            return zonesResponse.Zones.Where(x => x.Paused is false).Select(z => new HetznerZone(z.Id, z.Name)).ToImmutableArray();
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
                return allZones.Where(x => x.Paused is false).Select(z => new HetznerZone(z.Id, z.Name)).ToImmutableArray();
            }

            allZones.AddRange(zonesResponse.Zones);
        }

        return allZones.Where(x => x.Paused is false).Select(z => new HetznerZone(z.Id, z.Name)).ToImmutableArray();
    }

    public async Task<HetznerZone?> GetZoneAsync(string zoneId)
    {
        var zoneResponse = await _httpClient.GetFromJsonAsync<Zone>($"zones/{zoneId}").ConfigureAwait(false);
        if (zoneResponse is null)
        {
            _log.Warning($"Zone with zone id {zoneId} not found in Hetzner DNS");
            return null;
        }

        return new HetznerZone(zoneResponse.Id, zoneResponse.Name);
    }

    public async Task<bool> CreateRecordAsync(HetznerRecord record)
    {
        using var response = await _httpClient.PostAsJsonAsync("records", record).ConfigureAwait(false);

        return response.IsSuccessStatusCode;
    }

    public async Task DeleteRecordAsync(HetznerRecord record)
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
