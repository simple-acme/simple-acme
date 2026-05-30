using System;
using System.Linq;
using System.Threading.Tasks;

using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;

using PKISharp.WACS.Plugins.ValidationPlugins.Dns.Internal;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns.Internal.Models;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [IPlugin.Plugin1<
        HetznerOptions, HetznerOptionsFactory,
        DnsValidationCapability, HetznerJson, HetznerArguments>
        ("7176cc8f-ba08-4b07-aa39-2f5d012c1d5a",
        "Hetzner", "Create verification records in Hetzner DNS",
        External = true)]
    public class Hetzner : DnsValidation<Hetzner>, IDisposable
    {
        private readonly HetznerOptions _options;

        private readonly IProxyService _proxyService;

        private readonly SecretServiceManager _secretServiceManager;

        private readonly ILogService _logService;

        private IHetznerClient? _client;

        public Hetzner(
            HetznerOptions options,
            IProxyService proxyService,
            LookupClientProvider dnsClient,
            SecretServiceManager ssm,
            ILogService logService,
            ISettings settings) : base(dnsClient, logService, settings)
        {
            _options = options;
            _proxyService = proxyService;
            _secretServiceManager = ssm;
            _logService = logService;
        }

        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            var client = await GetClientAsync();

            var zone = await this.GetHostedZone(client, record.Authority.Domain).ConfigureAwait(false);
            if (zone == null)
            {
                _log.Error("The zone could not be found using the Hetzner DNS API, thus creating a DNS validation record is impossible. " +
                    $"Please note you need to use an API Token, not the Global API Key. The token needs appropriate  permissions. " +
                    $"It is important, that this token has access to all zones in your account because we need to " +
                    $"list your zones. Read the docs carefully for instructions.");
                return false;
            }

            var host = record.Authority.Domain.Replace($".{zone.Name}", null);
            var txtRecord = new HetznerRecord("TXT", host, record.Value, zone.Id);

            return await client.CreateRecordAsync(txtRecord).ConfigureAwait(false);
        }

        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            var client = await GetClientAsync();

            try
            {
                var zone = await this.GetHostedZone(client, record.Authority.Domain).ConfigureAwait(false);
                if (zone == null)
                {
                    _log.Error("The zone could not be found using the Hetzner DNS API, thus creating a DNS validation record is impossible. " +
                        $"Please note you need to use an API Token, not the Global API Key. The token needs appropriate  permissions. " +
                        $"It is important, that this token has access to all zones in your account because we need to " +
                        $"list your zones. Read the docs carefully for instructions.");
                    return;
                }

                var host = record.Authority.Domain.Replace($".{zone.Name}", null);
                var txtRecord = new HetznerRecord("TXT", host, record.Value, zone.Id);

                await client.DeleteRecordAsync(txtRecord).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Unable to delete record from Hetzner DNS");
            }
        }

        public void Dispose() => _client?.Dispose();

        private async Task<HetznerZone?> GetHostedZone(IHetznerClient client, string recordName)
        {
            if (String.IsNullOrWhiteSpace(_options.ZoneId) is false)
            {
                _log.Debug("Using Zone Id specified by input arguments to get zone information.");

                return await client.GetZoneAsync(_options.ZoneId).ConfigureAwait(false);
            }

            _log.Debug($"Try getting best matching zone for record '{recordName}'.");

            var zones = await client.GetAllActiveZonesAsync().ConfigureAwait(false);
            if (zones.Count == 0)
            {
                _log.Error("No zones could be found using the Hetzner DNS API. " +
                    "Maybe you entered a wrong API Token?");
                throw new InvalidOperationException("No zones could be found using the Hetzner DNS API. Maybe you entered a wrong API Token?");
            }

            var bestZone = FindBestMatch(zones.ToDictionary(x => x.Name), recordName);
            if (bestZone == null)
            {
                _log.Error($"No zone could be found that matches with record {recordName} and is not paused. " +
                    $"Maybe the API Token does not allow access to your domain?");
                throw new Exception();
            }

            _log.Information($"Best matching zone found: {bestZone.Name}");

            return bestZone;
        }

        private async Task<IHetznerClient> GetClientAsync()
        {
            if (_client is not null)
            {
                return _client;
            }

            var apiToken = await _secretServiceManager.EvaluateSecret(_options.ApiToken)
                ?? throw new InvalidOperationException("API Token cannot be null");

            var httpClient = await _proxyService.GetHttpClient();

            _client = _options.UseHetznerCloud
                ? new HetznerCloudDnsClient(apiToken, _logService, httpClient)
                : new HetznerDnsClient(apiToken, _logService, httpClient);

            return _client;
        }
    }
}
