﻿using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns;
using PKISharp.WACS.Plugins.ValidationPlugins.DnsMadeEasy;
using PKISharp.WACS.Services;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{

    [IPlugin.Plugin1<
        DnsMadeEasyOptions, DnsMadeEasyOptionsFactory,
        DnsValidationCapability, DnsMadeEasyJson, DnsMadeEasyArguments>
        ("13993334-2d74-4ff6-801b-833b99bf231d",
        "DnsMadeEasy", "Create verification records in DnsMadeEasy DNS", 
        Name = "DNS Made Easy", External = true, Provider = "DigiCert")]
    internal class DnsMadeEasyDnsValidation(
        LookupClientProvider dnsClient,
        ILogService logService,
        ISettings settings,
        DomainParseService domainParser,
        DnsMadeEasyOptions options,
        SecretServiceManager ssm,
        IProxyService proxyService) : DnsValidation<DnsMadeEasyDnsValidation, DnsManagementClient>(dnsClient, logService, settings, proxyService)
    {
        protected override async Task<DnsManagementClient> CreateClient(HttpClient client)
        {
            return new(
                await ssm.EvaluateSecret(options.ApiKey) ?? "",
                await ssm.EvaluateSecret(options.ApiSecret) ?? "",
                client);
        }

        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            try
            {
                var client = await GetClient();
                var domain = domainParser.GetRegisterableDomain(record.Authority.Domain);
                var recordName = RelativeRecordName(domain, record.Authority.Domain);
                await client.CreateRecord(domain, recordName, RecordType.TXT, record.Value);
                return true;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Unable to create record at DnsMadeEasy");
                return false;
            }
        }

        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            try
            {
                var client = await GetClient();
                var domain = domainParser.GetRegisterableDomain(record.Authority.Domain);
                var recordName = RelativeRecordName(domain, record.Authority.Domain);
                await client.DeleteRecord(domain, recordName, RecordType.TXT);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Unable to delete record from DnsMadeEasy");
            }
        }
    }
}
