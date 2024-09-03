using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns;
using PKISharp.WACS.Plugins.ValidationPlugins.Simply;
using PKISharp.WACS.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    [IPlugin.Plugin1<
        SimplyOptions, SimplyOptionsFactory,
        DnsValidationCapability, SimplyJson, SimplyArguments>
        ("3693c40c-7c2f-4b70-aead-27869d8cbdf3", 
        "Simply", "Create verification records in Simply DNS", "Simply.com")]
    internal class SimplyDnsValidation(
        LookupClientProvider dnsClient,
        ILogService logService,
        ISettingsService settings,
        IProxyService proxyService,
        SecretServiceManager ssm,
        SimplyOptions options) : DnsValidation<SimplyDnsValidation>(dnsClient, logService, settings)
    {
        private readonly SimplyDnsClient _client = new(
                options.Account ?? "",
                ssm.EvaluateSecret(options.ApiKey) ?? "",
                proxyService.GetHttpClient());

        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            try
            {
                var recordName = record.Authority.Domain;
                var product = await GetProductAsync(recordName);
                if (product.Object == null)
                {
                    throw new InvalidOperationException();
                }
                await _client.CreateRecordAsync(product.Object, recordName, record.Value);
                return true;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Unable to create record at Simply");
            }
            return false;
        }

        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            try
            {
                var recordName = record.Authority.Domain;
                var product = await GetProductAsync(recordName);
                if (product.Object == null)
                {
                    throw new InvalidOperationException();
                }
                await _client.DeleteRecordAsync(product.Object, record.Authority.Domain, record.Value);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Unable to delete record from Simply");
            }
        }

        private async Task<Product> GetProductAsync(string recordName)
        {
            var products = await _client.GetAllProducts();
            var product = FindBestMatch(products.ToDictionary(x => x.Domain?.NameIdn ?? "", x => x), recordName);
            return product is null ? throw new Exception($"Unable to find product for record '{recordName}'") : product;
        }
    }
}
