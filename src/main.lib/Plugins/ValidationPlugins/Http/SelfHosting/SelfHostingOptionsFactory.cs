using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    internal class SelfHostingOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<SelfHostingOptions>
    {
        private ArgumentResult<int?> ValidationPort =>
            arguments.GetInt<SelfHostingArguments>(x => x.ValidationPort);

        private ArgumentResult<string?> ValidationProtocol =>
            arguments.GetString<SelfHostingArguments>(x => x.ValidationProtocol);

        public override async Task<SelfHostingOptions?> Default()
        {
            return new SelfHostingOptions()
            {
                Port = await ValidationPort.GetValue(),
                Https = (await ValidationProtocol.GetValue())?.ToLower() == "https" ? true : null
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(SelfHostingOptions options)
        {
            yield return (ValidationPort.Meta, options.Port);
            if (options.Https == true) 
            {
                yield return (ValidationProtocol.Meta, "https");
            }
        }
    }
}