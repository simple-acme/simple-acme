using PKISharp.WACS.Clients;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.InstallationPlugins
{
    [IPlugin.Plugin1<
        ScriptOptions, ScriptOptionsFactory, 
        InstallationCapability, WacsJsonPlugins, ScriptArguments>
        ("3bb22c70-358d-4251-86bd-11858363d913", 
        "Script", "Start external script or program")]
    internal partial class Script(
        Renewal renewal, ScriptOptions options,
        ScriptClient client, SecretServiceManager secretManager) : IInstallationPlugin
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
                var parameters = ReplaceParameters(options.ScriptParameters ?? "", defaultStoreInfo, newCertificate, oldCertificate, false);
                var censoredParameters = ReplaceParameters(options.ScriptParameters ?? "", defaultStoreInfo, newCertificate, oldCertificate, true);
                return await client.RunScript(options.Script, parameters, censoredParameters);
            }
            return false;
        }

        internal string ReplaceParameters(string input, StoreInfo? defaultStoreInfo, ICertificateInfo newCertificate, ICertificateInfo? oldCertificate, bool censor)
        {
            // Numbered parameters for backwards compatibility only,
            // do not extend for future updates
            var cachedCertificate = newCertificate as CertificateInfoCache;
            return TemplateRegex().Replace(input, (m) => {
                return m.Value switch
                {
                    "{0}" or "{CertCommonName}" => newCertificate.CommonName?.Value ?? "",
                    "{1}" or "{CachePassword}" => (censor ? renewal.PfxPassword?.DisplayValue : renewal.PfxPassword?.Value) ?? "",
                    "{2}" or "{CacheFile}" => cachedCertificate?.CacheFile.FullName ?? "",
                    "{3}" or "{StorePath}" => defaultStoreInfo?.Path ?? "",
                    "{4}" or "{CertFriendlyName}" => newCertificate.FriendlyName,
                    "{5}" or "{CertThumbprint}" => newCertificate.Thumbprint,
                    "{6}" or "{CacheFolder}" => cachedCertificate?.CacheFile.Directory?.FullName ?? "",
                    "{7}" or "{RenewalId}" => renewal.Id,
                    "{StoreType}" => defaultStoreInfo?.Name ?? "",
                    "{OldCertCommonName}" => oldCertificate?.CommonName?.Value ?? "",
                    "{OldCertFriendlyName}" => oldCertificate?.FriendlyName ?? "",
                    "{OldCertThumbprint}" => oldCertificate?.Thumbprint ?? "",
                    var s when s.StartsWith($"{{{SecretServiceManager.VaultPrefix}") => 
                        censor ? s : secretManager.EvaluateSecret(s.Trim('{', '}')) ?? s,
                    _ => m.Value
                };
            });
        }

        [GeneratedRegex("{.+?}")]
        private static partial Regex TemplateRegex();
    }
}
