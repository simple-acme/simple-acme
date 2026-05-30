using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [IPlugin.Plugin1<
        CzechiaOptions, CzechiaOptionsFactory,
        DnsValidationCapability, CzechiaJson, CzechiaArguments>
        ("837692a9-25bc-4adc-908e-dc3316f11e32",
        "Czechia", "Create verification records in Czechia DNS",
        External = true)]
    public class Czechia(
        CzechiaOptions options,
        IProxyService proxyService,
        LookupClientProvider dnsClient,
        SecretServiceManager ssm,
        ILogService log,
        ISettings settings,
        DomainParseService domainParseService) : DnsValidation<Czechia, HttpClient>(dnsClient, log, settings, proxyService)
    {
        protected override async Task<HttpClient> CreateClient(HttpClient client)
        {
            var token = await ssm.EvaluateSecret(options.ApiToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new Exception("Czechia API token missing");
            }

            client.DefaultRequestHeaders.Remove("AuthorizationToken");
            client.DefaultRequestHeaders.Add("AuthorizationToken", token);
            return client;
        }

        private sealed record TxtBody(string hostName, string text, int ttl, int publishZone);

        private string DetermineZone(DnsValidationRecord record)
        {
            var zone = options.ZoneName;
            if (string.IsNullOrWhiteSpace(zone))
            {
                zone = domainParseService.GetRegisterableDomain(record.Authority.Domain);
            }

            if (string.IsNullOrWhiteSpace(zone))
            {
                throw new Exception($"Unable to determine zone for {record.Authority.Domain}");
            }

            return zone.Trim().TrimEnd('.');
        }

        private static string NormalizeHost(string? host) =>
            string.IsNullOrWhiteSpace(host) ? "@" : host;

        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            try
            {
                var zone = DetermineZone(record);
                var host = NormalizeHost(RelativeRecordName(zone, record.Authority.Domain));
                var apiBaseUri = options.ApiBaseUri ?? "https://api.czechia.com/api";
                var ttl = options.Ttl ?? 3600;

                var ctx = await GetClient();
                var url = $"{apiBaseUri.TrimEnd('/')}/DNS/{Uri.EscapeDataString(zone)}/TXT";
                var body = new TxtBody(host, record.Value, ttl, publishZone: 1);

                using var resp = await ctx.PostAsJsonAsync(url, body).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    var msg = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _log.Error($"Czechia DNS POST failed: {(int)resp.StatusCode} {resp.ReasonPhrase} {msg}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Unhandled exception when attempting to create record");
                return false;
            }
        }

        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            try
            {
                var zone = DetermineZone(record);
                var host = NormalizeHost(RelativeRecordName(zone, record.Authority.Domain));
                var apiBaseUri = options.ApiBaseUri ?? "https://api.czechia.com/api";
                var ttl = options.Ttl ?? 3600;

                var ctx = await GetClient();
                var url = $"{apiBaseUri.TrimEnd('/')}/DNS/{Uri.EscapeDataString(zone)}/TXT";
                var body = new TxtBody(host, record.Value, ttl, publishZone: 1);

                using var req = new HttpRequestMessage(HttpMethod.Delete, url)
                {
                    Content = JsonContent.Create(body)
                };

                using var resp = await ctx.SendAsync(req).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    var msg = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _log.Warning($"Czechia DNS DELETE failed: {(int)resp.StatusCode} {resp.ReasonPhrase} {msg}");
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Unable to delete record");
            }
        }
    }
}
