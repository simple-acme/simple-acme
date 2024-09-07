using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
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
    internal sealed class TransIp : DnsValidation<TransIp>
    {
        private readonly DnsService _dnsService;
        private readonly DomainParseService _domainParser;

        public TransIp(
            LookupClientProvider dnsClient,
            ILogService log,
            IProxyService proxy,
            ISettingsService settings,
            DomainParseService domainParser,
            SecretServiceManager ssm,
            TransIpOptions options) : base(dnsClient, log, settings)
        {
            var auth = new AuthenticationService(options.Login, ssm.EvaluateSecret(options.PrivateKey), proxy);
            _dnsService = new DnsService(auth, proxy);
            _domainParser = domainParser;
        }

        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            try
            {
                var domain = _domainParser.GetRegisterableDomain(record.Authority.Domain);
                var recordName = RelativeRecordName(domain, record.Authority.Domain);
                await _dnsService.CreateDnsEntry(
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
                var domain = _domainParser.GetRegisterableDomain(record.Authority.Domain);
                var recordName = RelativeRecordName(domain, record.Authority.Domain);
                await _dnsService.DeleteDnsEntry(
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