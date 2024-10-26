using FluentCloudflare.Abstractions.Builders;
using FluentCloudflare.Api;
using FluentCloudflare.Api.Entities;
using FluentCloudflare.Extensions;
using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [IPlugin.Plugin1<
        CloudflareOptions, CloudflareOptionsFactory,
        DnsValidationCapability, CloudflareJson, CloudflareArguments>
        ("73af2c2e-4cf1-4198-a4c8-1129003cfb75", 
        "Cloudflare", "Create verification records in Cloudflare DNS",
        External = true)]
    public class Cloudflare(
        CloudflareOptions options,
        IProxyService proxyService,
        LookupClientProvider dnsClient,
        SecretServiceManager ssm,
        ILogService log,
        ISettingsService settings) : DnsValidation<Cloudflare>(dnsClient, log, settings), IDisposable
    {
        private readonly HttpClient _hc = proxyService.GetHttpClient();

        private IAuthorizedSyntax GetContext() =>
            // avoid name collision with this class
            FluentCloudflare.Cloudflare.WithToken(ssm.EvaluateSecret(options.ApiToken));

        private async Task<Zone> GetHostedZone(IAuthorizedSyntax context, string recordName)
        {
            var page = 0;
            var allZones = new List<Zone>();
            var totalCount = int.MaxValue;
            while (allZones.Count < totalCount)
            {
                page++;
                var zonesResp = await context.Zones.List().PerPage(50).Page(page).ParseAsync(_hc).ConfigureAwait(false);
                if (!zonesResp.Success || zonesResp.ResultInfo.Count == 0)
                {
                    break;
                }
                totalCount = zonesResp.ResultInfo.TotalCount;
                allZones.AddRange(zonesResp.Unpack());
            }
          
            if (allZones.Count == 0)
            {
                _log.Error("No zones could be found using the Cloudflare API. " +
                    "Maybe you entered a wrong API Token?");
                throw new Exception();
            }
            var bestZone = FindBestMatch(allZones.ToDictionary(x => x.Name), recordName);
            if (bestZone == null)
            {
                _log.Error($"No zone could be found that matches with record {recordName}. " +
                    $"Maybe the API Token does not allow access to your domain?");
                throw new Exception();
            }
            return bestZone;
        }

        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            var ctx = GetContext();
            var zone = await GetHostedZone(ctx, record.Authority.Domain).ConfigureAwait(false);
            if (zone == null)
            {
                _log.Error("The zone could not be found using the Cloudflare API, thus creating a DNS validation record is impossible. " +
                    $"Please note you need to use an API Token, not the Global API Key. The token needs the permissions Zone.Zone:Read and Zone.DNS:Edit. Regarding " +
                    $"Zone:Read it is important, that this token has access to all zones in your account (Zone Resources > Include > All zones) because we need to " +
                    $"list your zones. Read the docs carefully for instructions.");
                return false;
            }

            var dns = ctx.Zone(zone).Dns;
            _ = await dns.Create(DnsRecordType.TXT, record.Authority.Domain, record.Value)
                .Ttl(60)
                .CallAsync(_hc)
                .ConfigureAwait(false);
            return true;
        }

        private async Task DeleteRecord(string recordName, string token, IAuthorizedSyntax context, Zone zone)
        {
            var dns = context.Zone(zone).Dns;
            var records = await dns
                .List()
                .OfType(DnsRecordType.TXT)
                .WithName(recordName)
                .WithContent(token)
                .Match(MatchType.All)
                .CallAsync(_hc)
                .ConfigureAwait(false);
            var record = records.FirstOrDefault();
            if (record == null)
            {
                _log.Warning($"The record {recordName} that should be deleted does not exist at Cloudflare.");
                return;
            }

            try
            {
                _ = await dns.Delete(record.Id)
                    .CallAsync(_hc)
                    .ConfigureAwait(false);
            } 
            catch (Exception ex)
            {
                _log.Warning(ex, $"Unable to delete record from Cloudflare");
            }

        }

        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            var ctx = GetContext();
            var zone = await GetHostedZone(ctx, record.Authority.Domain).ConfigureAwait(false);
            await DeleteRecord(record.Authority.Domain, record.Value, ctx, zone);
        }

        public void Dispose()
        {
            _hc.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
