using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Tls
{
    internal class SelfHostingOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<SelfHostingOptions>
    {
        private ArgumentResult<int?> HostingPort => 
            arguments.GetInt<SelfHostingArguments>(x => x.ValidationPort);

        public override int Order => 100;

        public override async Task<SelfHostingOptions?> Default()
        {
            return new SelfHostingOptions()
            {
                Port = await HostingPort.GetValue(),
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(SelfHostingOptions options)
        {
            yield return (arguments.GetString<MainArguments>(x => x.ValidationMode).Meta, "tls-alpn-01");
            yield return (HostingPort.Meta, options.Port);
        }
    }
}