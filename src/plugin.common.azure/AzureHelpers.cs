using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.ResourceManager;
using PKISharp.WACS.Services;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.Azure.Common
{
    public class AzureHelpers(
        IAzureOptionsCommon options,
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
        public async Task<TokenCredential> GetTokenCredential()
        {
            var tokenOptions = new TokenCredentialOptions() { AuthorityHost = AzureAuthorityHost };
            if (options.UseMsi && string.IsNullOrEmpty(options.ClientId))
            {
                return new ManagedIdentityCredential(options: tokenOptions);
            }
            if (options.UseMsi && !string.IsNullOrEmpty(options.ClientId))
            {
                return new ManagedIdentityCredential(options.ClientId, options: tokenOptions);
            }
            var clientSecret = await ssm.EvaluateSecret(options.Secret?.Value);
            return new ClientSecretCredential(
                    options.TenantId,
                    options.ClientId,
                    clientSecret,
                    options: tokenOptions);
        }

        public ArmClientOptions ArmOptions(HttpClient client)
        {
            return new ArmClientOptions()
            {
                Environment = ArmEnvironment,
                Transport = new HttpClientTransport(client)
            };
        }
    }
}
