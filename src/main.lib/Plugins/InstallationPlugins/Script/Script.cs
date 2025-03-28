using PKISharp.WACS.Clients;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.InstallationPlugins
{
    [IPlugin.Plugin1<
        ScriptOptions, ScriptOptionsFactory, 
        InstallationCapability, WacsJsonPlugins, ScriptArguments>
        ("3bb22c70-358d-4251-86bd-11858363d913", 
        "Script", "Start external script or program", 
        Name = "Custom script")]
    internal partial class Script(
        Renewal renewal, 
        ScriptOptions options, 
        ScriptClient client, 
        SecretServiceManager secretServiceManager) : IInstallationPlugin
    {
        public async Task<bool> Install(Dictionary<Type, StoreInfo> storeInfo, ICertificateInfo newCertificate, ICertificateInfo? oldCertificate)
        {
            if (options.Script != null)
            {
                var defaultStoreInfo = default(StoreInfo?);
                if (storeInfo.Count != 0)
                {
                    defaultStoreInfo = storeInfo.First().Value;
                }
                var parameters = await ReplaceParameters(options.ScriptParameters ?? "", defaultStoreInfo, newCertificate, oldCertificate, false);
                var censoredParameters = await ReplaceParameters(options.ScriptParameters ?? "", defaultStoreInfo, newCertificate, oldCertificate, true);
                var result = await client.RunScript(options.Script, parameters, censoredParameters);
                return result.Success;
            }
            return false;
        }

        internal async Task<string> ReplaceParameters(string input, StoreInfo? defaultStoreInfo, ICertificateInfo newCertificate, ICertificateInfo? oldCertificate, bool censor)
        {
            var cachedCertificate = newCertificate as CertificateInfoCache;
            var replacements = new Dictionary<string, string?>
            {
                { "RenewalId", renewal.Id },
                { "CacheFile", cachedCertificate?.CacheFile.FullName },
                { "CacheFolder", cachedCertificate?.CacheFile.Directory?.FullName },
                { "CachePassword", censor ? renewal.PfxPassword?.DisplayValue : renewal.PfxPassword?.Value },
                { "CertCommonName", newCertificate.CommonName?.Value },
                { "CertFriendlyName", newCertificate.FriendlyName },
                { "CertThumbprint", newCertificate.Thumbprint },
                { "StorePath", defaultStoreInfo?.Path },
                { "StoreType", defaultStoreInfo?.Name },
                { "OldCertCommonName",oldCertificate?.CommonName?.Value },
                { "OldCertFriendlyName", oldCertificate?.FriendlyName },
                { "OldCertThumbprint", oldCertificate?.Thumbprint }
            };

            // Numbered parameters for backwards compatibility only,
            // do not extend for future updates
            replacements["0"] = replacements["CertCommonName"];
            replacements["1"] = replacements["CachePassword"];
            replacements["2"] = replacements["CacheFile"];
            replacements["3"] = replacements["StorePath"];
            replacements["4"] = replacements["CertFriendlyName"];
            replacements["5"] = replacements["CertThumbprint"];
            replacements["6"] = replacements["CacheFolder"];
            replacements["7"] = replacements["RenewalId"];

            return await ScriptClient.ReplaceTokens(input, replacements, secretServiceManager, censor);
        }
    }
}
