using Azure.Security.KeyVault.Certificates;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Azure.Common;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    /// <summary>
    /// Handle creation of DNS records in Azure
    /// </summary>
    [IPlugin.Plugin1<
        KeyVaultOptions, KeyVaultOptionsFactory, 
        DefaultCapability, KeyVaultJson, KeyVaultArguments>
        ("dbfa91e2-28c0-4b37-857c-df6575dbb388", 
        "KeyVault", "Store in Azure Key Vault", 
        Name = "Azure Key Vault", External = true, Provider = "Microsoft")]
    internal class KeyVault(KeyVaultOptions options, SecretServiceManager ssm, IProxyService proxyService, ILogService log) : IStorePlugin
    {
        private readonly AzureHelpers _helpers = new(options, ssm);

        public Task Delete(ICertificateInfo certificateInfo) => Task.CompletedTask;

        public async Task<StoreInfo?> Save(ICertificateInfo certificateInfo)
        {
            var token = await _helpers.GetTokenCredential();
            var httpClient = await proxyService.GetHttpClient();
            var armOptions = _helpers.ArmOptions(httpClient);
            var client = new CertificateClient(
                new Uri($"https://{options.VaultName}.vault.azure.net/"),
                token,
                new CertificateClientOptions() {
                    Transport = armOptions.Transport
                });
            var importOptions = new ImportCertificateOptions(
                options.CertificateName,
                certificateInfo.PfxBytes());
            try
            {
                _ = await client.ImportCertificateAsync(importOptions);
                return new StoreInfo() {
                    Path = options.VaultName,
                    Name = options.CertificateName
                };
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error importing certificate to KeyVault");
            }
            return null;
        }
    }
}