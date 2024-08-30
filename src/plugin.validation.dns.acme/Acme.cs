using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [IPlugin.Plugin1<
        AcmeOptions, AcmeOptionsFactory, 
        DnsValidationCapability, AcmeJson, AcmeArguments>
        ("c13acc1b-7571-432b-9652-7a68a5f506c5", 
        "acme-dns", "Create verification records with acme-dns (https://github.com/joohoi/acme-dns)")]
    public class Acme(
        LookupClientProvider dnsClient,
        ILogService log,
        ISettingsService settings,
        IInputService input,
        IProxyService proxy,
        AcmeOptions options) : DnsValidation<Acme>(dnsClient, log, settings)
    {

        /// <summary>
        /// Send API call to the acme-dns server
        /// </summary>
        /// <param name="recordName"></param>
        /// <param name="token"></param>
        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            var client = new AcmeDnsClient(_dnsClient, proxy, _log, _settings, input, new Uri(options.BaseUri!));
            return await client.Update(record.Context.Identifier, record.Value);
        }

        public override Task DeleteRecord(DnsValidationRecord record) => Task.CompletedTask;
    }
}
