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

        /// <summary>
        /// Token endpoint may be different based on chosen region
        /// </summary>
        /// <returns></returns>
        private Uri AzureAuthorityHost
        {
            get
            {
                if (ArmEnvironment == ArmEnvironment.AzureChina)
                {
                    return AzureAuthorityHosts.AzureChina;
                }
                else if (ArmEnvironment == ArmEnvironment.AzureGermany)
                {
                    return AzureAuthorityHosts.AzureGermany;
                }
                else if (ArmEnvironment == ArmEnvironment.AzureGovernment)
                {
                    return AzureAuthorityHosts.AzureGovernment;
                }
                return AzureAuthorityHosts.AzurePublicCloud;
            }
        }

        /// <summary>
        /// Create the right type of TokenCredential based on user preferences
        /// </summary>
        public TokenCredential TokenCredential
        {
            get
            {
                var tokenOptions = new TokenCredentialOptions() { AuthorityHost = AzureAuthorityHost };
                return options.UseMsi
                    ? new ManagedIdentityCredential(options: tokenOptions)
                    : new ClientSecretCredential(
                          options.TenantId,
                          options.ClientId,
                          ssm.EvaluateSecret(options.Secret?.Value),
                          options: tokenOptions);
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
