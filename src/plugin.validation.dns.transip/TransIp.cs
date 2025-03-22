using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using TransIp.Library;
using TransIp.Library.Dto;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [IPlugin.Plugin1<
        TransIpOptions, TransIpOptionsFactory, 
        DnsValidationCapability, TransIpJson, TransIpArguments>
        ("c49a7a9a-f8c9-494a-a6a4-c6b9daae7d9d", 
        "TransIP", "Create verification records at TransIP",
        External = true)]
    internal sealed class TransIp(
        LookupClientProvider dnsClient,
        ILogService log,
        IProxyService proxy,
        ISettingsService settings,
        DomainParseService domainParser,
        SecretServiceManager ssm,
        TransIpOptions options) : DnsValidation<TransIp, DnsService>(dnsClient, log, settings, proxy)
    {
        protected override async Task<DnsService> CreateClient(HttpClient httpClient)
        {
            var auth = new AuthenticationService(options.Login, await ssm.EvaluateSecret(options.PrivateKey), httpClient);
            return new DnsService(auth, httpClient);
        }

        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            try
            {
                var domain = domainParser.GetRegisterableDomain(record.Authority.Domain);
                var recordName = RelativeRecordName(domain, record.Authority.Domain);
                var dnsService = await GetClient();
                await dnsService.CreateDnsEntry(
                    domain,
                    new DnsEntry()
                    {
                        Content = record.Value,
                        Name = recordName,
                        Type = "TXT"
                    });
                return true;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Error creating TXT record");
                return false;
            }
        }

        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            try
            {
                var dnsService = await GetClient();
                var domain = domainParser.GetRegisterableDomain(record.Authority.Domain);
                var recordName = RelativeRecordName(domain, record.Authority.Domain);
                await dnsService.DeleteDnsEntry(
                    domain,
                    new DnsEntry()
                    {
                        Content = record.Value,
                        Name = recordName,
                        Type = "TXT"
                    });
            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Error deleting TXT record");
            }
        }
    }
}