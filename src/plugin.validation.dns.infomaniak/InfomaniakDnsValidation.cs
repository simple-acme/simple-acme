using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns;

/// <summary>
/// Handle creation of DNS records in Infomaniak
/// </summary>
[IPlugin.Plugin1<
    InfomaniakOptions, InfomaniakOptionsFactory,
    DnsValidationCapability, InfomaniakJson, InfomaniakArguments>
    ("d1401d1e-b925-407d-a38b-924d67b2d1b5",
    "Infomaniak", "Create verification records in Infomaniak DNS")]
internal class InfomaniakDnsValidation(
    LookupClientProvider dnsClient,
    ILogService logService,
    ISettingsService settings,
    DomainParseService domainParser,
    InfomaniakOptions options,
    SecretServiceManager ssm,
    IProxyService proxyService) : DnsValidation<InfomaniakDnsValidation>(dnsClient, logService, settings)
{
    private readonly InfomaniakClient _client = new(ssm.EvaluateSecret(options.ApiToken) ?? "", logService, proxyService);
    private readonly Dictionary<string, int> _domainIds = [];
    private readonly ConcurrentDictionary<int, List<int>> _recordIds = new();

    public override async Task<bool> CreateRecord(DnsValidationRecord record)
    {
        try
        {
            var domain = domainParser.GetRegisterableDomain(record.Authority.Domain);
            var domainId = await _client.GetDomainId(domain);
            if (domainId == 0)
            {
                throw new InvalidDataException("Infomaniak did not return a valid domain id.");
            }
            _ = _domainIds.TryAdd(record.Authority.Domain, domainId);

            var recordName = RelativeRecordName(domain, record.Authority.Domain);
            var recordId = await _client.CreateRecord(domainId, recordName, record.Value);
            if (recordId == 0)
            {
                throw new InvalidDataException("Infomaniak did not return a valid domain record id.");
            }
            _ = _recordIds.AddOrUpdate(
                domainId,
                [recordId],
                (b, s) => [.. s, recordId]);
            return true;
        }
        catch (Exception ex)
        {
            _log.Warning(ex, $"Unable to create record at Infomaniak");
            return false;
        }
    }

    public override async Task DeleteRecord(DnsValidationRecord record)
    {
        if (_domainIds.TryGetValue(record.Authority.Domain, out var domainId) && _recordIds.TryGetValue(domainId, out var recordIds))
        {
            foreach (var recordId in recordIds)
            {
                try
                {
                    await _client.DeleteRecord(domainId, recordId);
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "Unable to delete record {recordId} from Infomaniak domain {domainId}: {message}", recordId, domainId);
                }
            }
        }
    }

}
