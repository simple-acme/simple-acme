﻿using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns.NS1;
using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [IPlugin.Plugin1<
        NS1Options, NS1OptionsFactory,
        DnsValidationCapability, NS1Json, NS1Arguments>
        ("C66CC8BE-3046-46C2-A0BA-EC4EC3E7FE96", 
        "NS1", "Create verification records in NS1 DNS", 
        Name = "NS1/NSONE", External = true, Provider = "IBM")]
    internal class NS1DnsValidation(
        LookupClientProvider dnsClient,
        ILogService logService,
        ISettings settings,
        IProxyService proxy,
        NS1Options options,
        SecretServiceManager ssm) : DnsValidation<NS1DnsValidation, DnsManagementClient>(dnsClient, logService, settings, proxy)
    {
        protected override async Task<DnsManagementClient> CreateClient(HttpClient httpClient) => new(
            await ssm.EvaluateSecret(options.ApiKey) ?? "",
            httpClient);

        private static readonly Dictionary<string, string> _zonesMap = [];

        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            var client = await GetClient();
            var zones = await client.GetZones();
            if (zones == null)
            {
                _log.Error("Failed to get DNS zones list for account. Aborting.");
                return false;
            }

            var zone = FindBestMatch(zones.ToDictionary(x => x), record.Authority.Domain);
            if (zone == null)
            {
                _log.Error("No matching zone found in NS1 account. Aborting");
                return false;
            }
            _zonesMap[record.Authority.Domain] = zone;

            var result = await client.CreateRecord(zone, record.Authority.Domain, "TXT", record.Value);
            if (!result)
            {
                _log.Error("Failed to create DNS record. Aborting");
                return false;
            }

            return true;
        }

        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            var client = await GetClient();
            string zone;
            if (!_zonesMap.TryGetValue(record.Authority.Domain, out zone!))
            {
                _log.Warning($"No record with name {record.Authority.Domain} was created");
                return;
            }
            _ = _zonesMap.Remove(record.Authority.Domain);
            var result = await client.DeleteRecord(zone, record.Authority.Domain, "TXT");
            if (!result)
            {
                _log.Error("Failed to delete DNS record");
                return;
            }
        }
    }
}
