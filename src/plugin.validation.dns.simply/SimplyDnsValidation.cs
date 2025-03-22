using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns;
using PKISharp.WACS.Plugins.ValidationPlugins.Simply;
using PKISharp.WACS.Services;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    [IPlugin.Plugin1<
        SimplyOptions, SimplyOptionsFactory,
        DnsValidationCapability, SimplyJson, SimplyArguments>
        ("3693c40c-7c2f-4b70-aead-27869d8cbdf3", 
        "Simply", "Create verification records in Simply.com DNS", 
        Name = "Simply.com", External = true)]
    internal class SimplyDnsValidation(
        LookupClientProvider dnsClient,
        ILogService logService,
        ISettingsService settings,
        IProxyService proxyService,
        SecretServiceManager ssm,
        SimplyOptions options) : DnsValidation<SimplyDnsValidation, SimplyDnsClient>(dnsClient, logService, settings, proxyService)
    {
        protected override async Task<SimplyDnsClient> CreateClient(HttpClient httpClient) =>
            new (options.Account ?? "",
                await ssm.EvaluateSecret(options.ApiKey) ?? "",
                httpClient);

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
                var client = await GetClient();
                await client.CreateRecordAsync(product.Object, recordName, record.Value);
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
                var client = await GetClient();
                await client.DeleteRecordAsync(product.Object, record.Authority.Domain, record.Value);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Unable to delete record from Simply");
            }
        }

        private async Task<Product> GetProductAsync(string recordName)
        {
            var client = await GetClient();
            var products = await client.GetAllProducts();
            var product = FindBestMatch(products.ToDictionary(x => x.Domain?.NameIdn ?? "", x => x), recordName);
            return product is null ? throw new Exception($"Unable to find product for record '{recordName}'") : product;
        }
    }
}
