using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns;
using PKISharp.WACS.Services;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins;

/// <summary>
/// DNS validation plugin for ip-projects.de
/// </summary>
[IPlugin.Plugin1<IPProjectsOptions, IPProjectsOptionsFactory,
                 DnsValidationCapability, IPProjectsJson, IPProjectsArguments>
                ("6370923c-7840-4e8c-b003-a3b514312e5a",
                 "IPProjects",
                 "Create verification records at ip-projects.de",
                 External = true,
                 Provider = null)]
internal class IPProjects(IPProjectsOptions options,
                          LookupClientProvider dnsClient,
                          ILogService log,
                          ISettings settings,
                          IProxyService proxy,
                          SecretServiceManager ssm,
                          DomainParseService domainParser)
    : DnsValidation<IPProjects, IPProjectsClient>(dnsClient, log, settings, proxy)
{
    protected override async Task<IPProjectsClient> CreateClient(HttpClient httpClient)
    {
        string apiKey = await ssm.EvaluateSecret(options.ApiKey) ?? "";
        return new IPProjectsClient(httpClient, apiKey);
    }

    public override async Task<bool> CreateRecord(DnsValidationRecord record)
    {
        try
        {
            string domain = domainParser.GetRegisterableDomain(record.Authority.Domain);
            string recordName = RelativeRecordName(domain, record.Authority.Domain);
            recordName = recordName == "@" ? string.Empty : recordName;

            IPProjectsClient client = await GetClient();
            return await client.AddRecord(domain, recordName, record.Value);
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
            string domain = domainParser.GetRegisterableDomain(record.Authority.Domain);
            string recordName = RelativeRecordName(domain, record.Authority.Domain);
            recordName = recordName == "@" ? string.Empty : recordName;

            IPProjectsClient client = await GetClient();
            await client.DeleteRecord(domain, recordName, record.Value);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Unhandled exception when attempting to delete record");
        }
    }
}
