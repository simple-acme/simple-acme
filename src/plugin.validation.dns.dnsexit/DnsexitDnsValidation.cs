using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns;
using PKISharp.WACS.Plugins.ValidationPlugins.Dnsexit;
using PKISharp.WACS.Services;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    [IPlugin.Plugin1<
        DnsexitOptions, DnsexitOptionsFactory, 
        DnsValidationCapability, DnsexitJson, DnsexitArguments>
        ("C9017182-1000-4257-A8DA-0553CD1490EC", 
        "DNSExit", "Create verification records in Dnsexit DNS")]
    internal class DnsExitDnsValidation(
        LookupClientProvider dnsClient,
        ILogService logService,
        ISettingsService settings,
        DomainParseService domainParser,
        DnsexitOptions options,
        SecretServiceManager ssm,
        IProxyService proxyService) : DnsValidation<DnsExitDnsValidation>(dnsClient, logService, settings)
    {
        private readonly DnsManagementClient _client = new(
                ssm.EvaluateSecret(options.ApiKey) ?? "",
                logService, proxyService);

        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            try
            {
                var domain = domainParser.GetRegisterableDomain(record.Authority.Domain);
                var recordName = RelativeRecordName(domain, record.Authority.Domain);
                await _client.CreateRecord(domain, recordName, RecordType.TXT, record.Value);
                return true;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Unable to create record at DnsExit");
                return false;
            }
        }

        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            try
            {
                var domain = domainParser.GetRegisterableDomain(record.Authority.Domain);
                var recordName = RelativeRecordName(domain, record.Authority.Domain);
                await _client.DeleteRecord(domain, recordName, RecordType.TXT);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Unable to delete record from DnsExit");
            }
        }
    }
}
