using PKISharp.WACS.Services;
using System;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.SecretPlugins
{
    /// <summary>
    /// Get secrets from the environment, i.e. "vault://environment/mysecret"
    /// Note that to use this properly, the environment should be configured
    /// for both the scheduled task and when setting up the renewals manually
    /// </summary>
    public class EnvironmentSecretService : ISecretProvider
    {
        public string Prefix => "environment";

        public Task<string?> GetSecret(string? key) => key != null ? Task.FromResult(Environment.GetEnvironmentVariable(key)) : Task.FromResult<string?>(null);
    }
}
