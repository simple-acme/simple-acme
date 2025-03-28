using PKISharp.WACS.Clients;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.SecretPlugins
{
    /// <summary>
    /// Get and set secrets via configurable script, can be used to connect to third party tools
    /// </summary>
    /// <param name="settings"></param>
    /// <param name="log"></param>
    public class ScriptSecretService(ILogService log, ISettingsService settings, ScriptClient scriptClient) : ISecretProvider
    {
        /// <summary>
        /// Make references to this provider unique from 
        /// references in other providers
        /// </summary>
        public string Prefix => "script";

        /// <summary>
        /// Read secret from the file
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public async Task<string?> GetSecret(string? identifier) 
        {
            var getScript = settings.Secrets?.Script?.Get;
            if (string.IsNullOrWhiteSpace(getScript))
            {
                log.Warning("Script vault called but not script configured for Get operation");
                return null;
            }
            var getScriptArgs = settings.Secrets?.Script?.GetArguments ?? "-key {key}";
            if (getScriptArgs?.Contains(SecretServiceManager.VaultPrefix + Prefix) == true)
            {
                throw new InvalidOperationException("Secret recursion not supported");
            }
            var replacements = new Dictionary<string, string?>
            {
                { "Key", identifier },
                { "Operation", "get" }
            };
            var actual = await ScriptClient.ReplaceTokens(getScriptArgs, replacements);
            var result = await scriptClient.RunScript(getScript, actual, hideOutput: true);
            return result.Output?.Trim();
        }
    }
}
