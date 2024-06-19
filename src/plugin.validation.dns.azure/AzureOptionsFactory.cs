using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Azure.Common;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    /// <summary>
    /// Azure DNS validation
    /// </summary>
    internal class AzureOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<AzureOptions>
    {
        private ArgumentResult<string?> SubscriptionId => arguments.
            GetString<AzureArguments>(a => a.AzureSubscriptionId);

        private ArgumentResult<string?> HostedZone => arguments.
             GetString<AzureArguments>(a => a.AzureHostedZone);

        public override async Task<AzureOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            var options = new AzureOptions();
            var common = new AzureOptionsFactoryCommon<AzureArguments>(arguments);
            await common.Aquire(options, input);
            options.SubscriptionId = await SubscriptionId.Interactive(input).GetValue();
            options.HostedZone = await HostedZone.Interactive(input).GetValue();
            return options;
        }

        public override async Task<AzureOptions?> Default()
        {
            var options = new AzureOptions();
            var common = new AzureOptionsFactoryCommon<AzureArguments>(arguments);
            await common.Default(options);
            options.SubscriptionId = await SubscriptionId.GetValue();
            options.HostedZone = await HostedZone.GetValue();
            return options;
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(AzureOptions options)
        {
            var common = new AzureOptionsFactoryCommon<AzureArguments>(arguments);
            foreach (var x in common.Describe(options))
            {
                yield return x;
            }
            yield return (SubscriptionId.Meta, options.SubscriptionId);
            yield return (HostedZone.Meta, options.HostedZone);
        }

    }
}
