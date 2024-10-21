using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.ResourceManager;
using PKISharp.WACS.Services;
using System;

namespace PKISharp.WACS.Plugins.Azure.Common
{
    public class AzureHelpers(
        IAzureOptionsCommon options,
        IProxyService proxy,
        SecretServiceManager ssm)
    {

        /// <summary>
        /// Retrieve active directory settings based on the current Azure environment
        /// </summary>
        /// <returns></returns>
        private ArmEnvironment ArmEnvironment
        {
            get {
                if (string.IsNullOrWhiteSpace(options.AzureEnvironment))
                {
                    return ArmEnvironment.AzurePublicCloud;
                }
                return options.AzureEnvironment switch
                {
                    AzureEnvironments.AzureChinaCloud => ArmEnvironment.AzureChina,
                    AzureEnvironments.AzureUSGovernment => ArmEnvironment.AzureGovernment,
                    AzureEnvironments.AzureGermanCloud => ArmEnvironment.AzureGermany,
                    AzureEnvironments.AzureCloud => ArmEnvironment.AzurePublicCloud,
                    null => ArmEnvironment.AzurePublicCloud,
                    "" => ArmEnvironment.AzurePublicCloud,
                    _ => new ArmEnvironment(new Uri(options.AzureEnvironment), options.AzureEnvironment)
                };
            }
        }

        public TokenCredential TokenCredential
        {
            get
            { 
                if (_options.UseMsi && string.IsNullOrEmpty(_options.ClientId)) {
                    return new ManagedIdentityCredential();
                } 

                if (_options.UseMsi && !string.IsNullOrEmpty(_options.ClientId)) {
                    return new ManagedIdentityCredential(_options.ClientId);
                } 

                return new ClientSecretCredential(
                        _options.TenantId,
                        _options.ClientId,
                        _ssm.EvaluateSecret(_options.Secret?.Value));
            }
        }

        public ArmClientOptions ArmOptions
        {
            get 
            {
                return new ArmClientOptions() { 
                    Environment = ArmEnvironment,
                    Transport = new HttpClientTransport(proxy.GetHttpClient())
                };
            }
        }
    }
}
