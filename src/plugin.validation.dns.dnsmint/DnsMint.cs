using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns;
using PKISharp.WACS.Services;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    [IPlugin.Plugin1<
        DnsMintOptions, DnsMintOptionsFactory,
        DnsValidationCapability, DnsMintJson, DnsMintArguments>
        ("87b5008b-62f2-4dd6-9fd7-30b46e4ddfd5",
        "DnsMint", "Create verification records in DNSMint",
        External = true)]
    internal class DnsMintValidation(
        DnsMintOptions options,
        LookupClientProvider dnsClient,
        ILogService log,
        ISettings settings,
        IProxyService proxy,
        SecretServiceManager ssm) : DnsValidation<DnsMintValidation, DnsMintClient>(dnsClient, log, settings, proxy)
    {
        protected override async Task<DnsMintClient> CreateClient(HttpClient httpClient)
        {
            var apiKey = await ssm.EvaluateSecret(options.ApiKey) ?? "";
            return new DnsMintClient(httpClient, apiKey);
        }

        /// <summary>
        /// The record name goes to the API unchanged. DNSMint works out which
        /// hostname it belongs to, so there is no zone lookup and no
        /// RelativeRecordName.
        /// </summary>
        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            try
            {
                var client = await GetClient();
                await client.CreateTxtRecord(record.Authority.Domain, record.Value);
                return true;
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
                var client = await GetClient();
                await client.DeleteTxtRecord(record.Authority.Domain, record.Value);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Unable to delete record");
            }
        }
    }
}
