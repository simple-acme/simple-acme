using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

using PKISharp.WACS.Services;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns.Internal.Models;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns.Internal.Models.HetznerCloud;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns.Internal;

internal sealed class HetznerCloudDnsClient : IHetznerClient, IDisposable
{
    private static readonly Uri BASE_ADDRESS = new Uri("https://api.hetzner.cloud/v1/");

    private readonly ILogService log;

    private readonly HttpClient httpClient;

    public HetznerCloudDnsClient(string apiToken, ILogService logService, IProxyService proxyService)
    {
        this.log = logService;

        this.httpClient = proxyService.GetHttpClient();
        this.httpClient.BaseAddress = BASE_ADDRESS;
        this.httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiToken}");
    }

    public void Dispose()
    {
        this.httpClient.Dispose();
    }

    public async Task<IReadOnlyCollection<HetznerZone>> GetAllActiveZonesAsync()
    {
        var zonesResponse = await this.httpClient.GetFromJsonAsync<ZonesResponse>("zones").ConfigureAwait(false);
        if (zonesResponse is null)
        {
            this.log.Warning("No zones found in Hetzner DNS");
            return Array.Empty<HetznerZone>();
        }

        // Is only one page returned?
        if (zonesResponse.Meta.Pagination.LastPage == zonesResponse.Meta.Pagination.Page)
        {
            return zonesResponse.Zones.Select(z => new HetznerZone(z.Id, z.Name)).ToImmutableArray();
        }

        var allZones = new List<Zone>();
        allZones.AddRange(zonesResponse.Zones);

        // As long as we are not on the last page continue
        while (zonesResponse.Meta.Pagination.LastPage != zonesResponse.Meta.Pagination.Page)
        {
            var nextPage = zonesResponse.Meta.Pagination.Page + 1;

            zonesResponse = await this.httpClient.GetFromJsonAsync<ZonesResponse>($"zones?page={nextPage}").ConfigureAwait(false);
            if (zonesResponse is null)
            {
                this.log.Warning($"No zones found on page {nextPage} in Hetzner DNS");
                return allZones.Select(z => new HetznerZone(z.Id, z.Name)).ToImmutableArray();
            }

            allZones.AddRange(zonesResponse.Zones);
        }

        return allZones.Select(z => new HetznerZone(z.Id, z.Name)).ToImmutableArray();
    }

    public async Task<HetznerZone?> GetZoneAsync(string zoneId)
    {
        var zoneResponse = await this.httpClient.GetFromJsonAsync<ZoneResponse>($"zones/{zoneId}").ConfigureAwait(false);
        if (zoneResponse is null)
        {
            this.log.Warning($"Zone with zone id {zoneId} not found in Hetzner DNS");
            return null;
        }

        return new HetznerZone(zoneResponse.Zone.Id, zoneResponse.Zone.Name);
    }

    public async Task<bool> CreateRecordAsync(HetznerRecord record)
    {
        var rrSet = new RRSet(record);
        using var response = await this.httpClient.PostAsJsonAsync($"zones/{record.zone_id}/rrsets", rrSet).ConfigureAwait(false);

        return response.IsSuccessStatusCode;
    }

    public async Task DeleteRecordAsync(HetznerRecord record)
    {
        using var deleteResponse = await this.httpClient.DeleteAsync($"zones/{record.zone_id}/rrsets/{record.name}/{record.type}").ConfigureAwait(false);

        deleteResponse.EnsureSuccessStatusCode();
    }
}
