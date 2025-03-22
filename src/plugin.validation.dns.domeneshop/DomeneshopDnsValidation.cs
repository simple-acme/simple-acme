using Abstractions.Integrations.Domeneshop;
using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns;
using PKISharp.WACS.Services;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Options = Abstractions.Integrations.Domeneshop.DomeneshopOptions;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    [IPlugin.Plugin1<
        DomeneshopOptions, DomeneshopOptionsFactory,
        DnsValidationCapability, DomeneshopJson, DomeneshopArguments>
        ("0BD9B320-08E0-4BFE-A535-B979886187E4",
        "Domeneshop", "Create verification records in Domeneshop DNS",
        External = true, Page = "domene")]
    internal class DomeneshopDnsValidation(
        LookupClientProvider dnsClient,
        ILogService logService,
        ISettingsService settings,
        IProxyService proxy,
        DomainParseService domainParser,
        DomeneshopOptions options,
        SecretServiceManager ssm) : DnsValidation<DomeneshopDnsValidation, DomeneshopClient>(dnsClient, logService, settings, proxy)
    {
        private readonly DomainParseService _domainParser = domainParser;
        private readonly ILogService _logService = logService;

        private Domain? domain;
        private DnsRecord? txt;

        protected override async Task<DomeneshopClient> CreateClient(HttpClient httpClient)
        {
            return new DomeneshopClient(new Options
            {
                ClientId = await ssm.EvaluateSecret(options.ClientId) ?? "",
                ClientSecret = await ssm.EvaluateSecret(options.ClientSecret) ?? "",
            });
        }

        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            try
            {
                var domainname = _domainParser.GetRegisterableDomain(record.Authority.Domain);
                var recordName = RelativeRecordName(domainname, record.Authority.Domain);
                var client = await GetClient();
                var domains = await client.GetDomainsAsync();
                domain = domains.FirstOrDefault(d => d.Name.Equals(domainname, StringComparison.OrdinalIgnoreCase));

                if (domain == null)
                {
                    _logService.Error("The following domain could not be found as one of the users domains: {0}", domainname);
                    return false;
                }

                txt = new DnsRecord(DnsRecordType.TXT, recordName, record.Value);
                txt = await client.EnsureDnsRecordAsync(domain.Id, txt);

                return true;
            }
            catch (Exception exception)
            {
                _logService.Error(exception, "Unhandled exception when attempting to create record");
                return false;
            }
        }

        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            try
            {
                var client = await GetClient();
                #pragma warning disable CS8602 // Dereference of a possibly null reference.
                #pragma warning disable CS8629 // Nullable value type may be null.
                await client.DeleteDnsRecordAsync(domain.Id, txt.Id.Value);
                #pragma warning restore CS8629 // Nullable value type may be null.
                #pragma warning restore CS8602 // Dereference of a possibly null reference.

            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Unable to delete record from Domeneshop");
            }
        }
    }
}
