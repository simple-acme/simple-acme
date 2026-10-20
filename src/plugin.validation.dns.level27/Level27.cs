using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns;
using PKISharp.WACS.Services;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    [IPlugin.Plugin1<
        Level27Options, Level27OptionsFactory,
        DnsValidationCapability, Level27Json, Level27Arguments>
        ("55891a42-ca55-4085-b107-b6a0c4f6f30b",
        "Level27", "Create verification records in Level27 DNS",
        External = true)]
    internal class Level27Validation(
        Level27Options options,
        LookupClientProvider dnsClient,
        ILogService log,
        ISettings settings,
        IProxyService proxy,
        SecretServiceManager ssm,
        DomainParseService domainParser) : DnsValidation<Level27Validation, Level27Client>(dnsClient, log, settings, proxy)
    {
        protected override async Task<Level27Client> CreateClient(HttpClient httpClient)
        {
            var apiKey = await ssm.EvaluateSecret(options.ApiKey) ?? "";
            return new Level27Client(httpClient, apiKey, options.ApiBaseUrl);
        }

        /// <summary>
        /// Create a DNS record required by the ACME server
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            try
            {
                var zone = await GetHostZone(record.Authority.Domain);
                if (zone == null)
                {
                    _log.Error("Unable to find zone for {challengeDomain}", record.Authority.Domain);
                    return false;
                }
                var host = RelativeRecordName(zone.Name, record.Authority.Domain);
                var client = await GetClient();
                _log.Debug("Creating TXT record for {host} with value {value}", host, record.Value);
                await client.CreateTxtRecord(zone, host, record.Value);
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Unhandled exception when attempting to create record");
                return false;
            }
        }

        /// <summary>
        /// Delete the TXT record after validation has been completed
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            try
            {
                var zone = await GetHostZone(record.Authority.Domain);
                if (zone == null)
                {
                    _log.Warning("Unable to find zone for {challengeDomain}", record.Authority.Domain);
                    return;
                }
                var client = await GetClient();
                var host = RelativeRecordName(zone.Name, record.Authority.Domain);
                _log.Debug("Deleting TXT record for {host} with value {value}", host, record.Value);
                await client.DeleteTxtRecord(zone, host, record.Value);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Unable to delete record");
            }
        }

        /// <summary>
        /// Select the best matching zone for the given challenge domain. A single
        /// query returns the registered domain as well as any delegated
        /// subdomains, so the most specific match is chosen.
        /// </summary>
        /// <param name="challengeDomain"></param>
        /// <returns></returns>
        private async Task<Level27Zone?> GetHostZone(string challengeDomain)
        {
            var client = await GetClient();
            var registeredDomain = domainParser.GetRegisterableDomain(challengeDomain);
            var zones = await client.GetZones(registeredDomain);
            return FindBestMatch(zones.ToDictionary(x => x.Name), challengeDomain);
        }
    }
}
