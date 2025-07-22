using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns;
using PKISharp.WACS.Plugins.ValidationPlugins.Dreamhost;
using PKISharp.WACS.Services;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    [IPlugin.Plugin1<
        DreamhostOptions, DreamhostOptionsFactory,
        DnsValidationCapability, DreamhostJson, DreamhostArguments>
        ("2bfb3ef8-64b8-47f1-8185-ea427b793c1a", 
        "DreamHost", "Create verification records in DreamHost DNS",
        External = true)]
    internal class DreamhostDnsValidation(
        LookupClientProvider dnsClient,
        ILogService logService,
        ISettings settings,
        IProxyService proxy,
        SecretServiceManager ssm,
        DreamhostOptions options) : DnsValidation<DreamhostDnsValidation, DnsManagementClient>(dnsClient, logService, settings, proxy)
    {
        protected override async Task<DnsManagementClient> CreateClient(HttpClient httpClient)
        {
            return new(await ssm.EvaluateSecret(options.ApiKey) ?? "", _log, httpClient);
        }

        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            try
            {
                var client = await GetClient();
                await client.CreateRecord(record.Authority.Domain, RecordType.TXT, record.Value);
                return true;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Unable to create record at Dreamhost");
                return false;
            }
        }

        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            try
            {
                var client = await GetClient();
                await client.DeleteRecord(record.Authority.Domain, RecordType.TXT, record.Value);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Unable to delete record from Dreamhost");
            }
        }
    }
}
