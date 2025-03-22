using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns;
using PKISharp.WACS.Plugins.ValidationPlugins.Godaddy;
using PKISharp.WACS.Services;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    [IPlugin.Plugin1<
        GodaddyOptions, GodaddyOptionsFactory, 
        DnsValidationCapability, GodaddyJson, GodaddyArguments>
        ("966c4c3d-1572-44c7-9134-5e2bc8fa021d", 
        "GoDaddy", "Create verification records in GoDaddy DNS",
        External = true)]
    internal class GodaddyDnsValidation(
        LookupClientProvider dnsClient,
        ILogService logService,
        ISettingsService settings,
        DomainParseService domainParser,
        GodaddyOptions options,
        SecretServiceManager ssm,
        IProxyService proxyService) : DnsValidation<GodaddyDnsValidation, DnsManagementClient>(dnsClient, logService, settings, proxyService)
    {
        protected override async Task<DnsManagementClient> CreateClient(HttpClient httpClient)
        {
            return new(
                await ssm.EvaluateSecret(options.ApiKey) ?? "",
                await ssm.EvaluateSecret(options.ApiSecret) ?? "",
                httpClient);
        }

        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            try
            {
                var domain = domainParser.GetRegisterableDomain(record.Authority.Domain);
                var recordName = RelativeRecordName(domain, record.Authority.Domain);
                var client = await GetClient();
                await client.CreateRecord(domain, recordName, RecordType.TXT, record.Value);
                return true;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Unable to create record at Godaddy");
                return false;
            }
        }

        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            try
            {
                var domain = domainParser.GetRegisterableDomain(record.Authority.Domain);
                var recordName = RelativeRecordName(domain, record.Authority.Domain);
                var client = await GetClient();
                await client.DeleteRecord(domain, recordName, RecordType.TXT);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Unable to delete record from Godaddy");
            }
        }
    }
}
