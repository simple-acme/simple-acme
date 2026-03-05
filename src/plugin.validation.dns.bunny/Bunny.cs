using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns;
using PKISharp.WACS.Services;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    [IPlugin.Plugin1<
        BunnyOptions, BunnyOptionsFactory,
        DnsValidationCapability, BunnyJson, BunnyArguments>
        ("86feb060-ac79-49d0-85c3-ef55687c2660",
        "Bunny", "Create verification records in Bunny.net DNS",
        External = true)]
    internal class BunnyValidation(
        BunnyOptions options,
        LookupClientProvider dnsClient,
        ILogService log,
        ISettings settings,
        IProxyService proxy,
        SecretServiceManager ssm,
        DomainParseService domainParser) : DnsValidation<BunnyValidation, BunnyClient>(dnsClient, log, settings, proxy)
    {
        protected override async Task<BunnyClient> CreateClient(HttpClient httpClient)
        {
            var APIKey = await ssm.EvaluateSecret(options.APIKey) ?? "";
            return new BunnyClient(httpClient, APIKey);
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
                var host = RelativeRecordName(zone.Domain, record.Authority.Domain);
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
                var host = RelativeRecordName(zone.Domain, record.Authority.Domain);
                _log.Debug("Deleting TXT record for {host} with value {value}", host, record.Value);
                await client.DeleteTxtRecord(zone, host, record.Value);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Unable to delete record");
            }
        }
        /// <summary>
        /// Retrieves the DNS zone associated with the specified domain.
        /// </summary>
        /// <param name="challengeDomain"></param>
        /// <returns>BunnyZone</returns>
        private async Task<BunnyZone?> GetHostZone(string challengeDomain)
        {
            var client = await GetClient();
            var registeredDomain = domainParser.GetRegisterableDomain(challengeDomain);
            var zone = await client.GetZone(registeredDomain);
            return zone;

        }
    }
}
