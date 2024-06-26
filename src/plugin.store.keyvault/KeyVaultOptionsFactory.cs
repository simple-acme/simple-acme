using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Azure.Common;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    /// <summary>
    /// Azure Key Vault
    /// </summary>
    internal class KeyVaultOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<KeyVaultOptions>()
    {
        private ArgumentResult<string?> VaultName => arguments.
            GetString<KeyVaultArguments>(a => a.VaultName).
            Required();

        private ArgumentResult<string?> CertificateName => arguments.
            GetString<KeyVaultArguments>(a => a.CertificateName).
            Required();

        public override async Task<KeyVaultOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            var options = new KeyVaultOptions();
            var common = new AzureOptionsFactoryCommon<KeyVaultArguments>(arguments);
            await common.Aquire(options, input);
            options.VaultName = await VaultName.Interactive(input).GetValue();
            options.CertificateName = await CertificateName.Interactive(input).GetValue();
            return options;
        }

        public override async Task<KeyVaultOptions?> Default()
        {
            var options = new KeyVaultOptions();
            var common = new AzureOptionsFactoryCommon<KeyVaultArguments>(arguments);
            await common.Default(options);
            options.VaultName = await VaultName.GetValue();
            options.CertificateName = await CertificateName.GetValue();
            return options;
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(KeyVaultOptions options)
        {
            var common = new AzureOptionsFactoryCommon<KeyVaultArguments>(arguments);
            foreach (var x in common.Describe(options))
            {
                yield return x;
            }
            yield return (CertificateName.Meta, options.CertificateName);
            yield return (VaultName.Meta, options.VaultName);
        }
    }
}
