using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    internal class BunnyOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<BunnyOptions>
    {
        private ArgumentResult<ProtectedString?> APIKey => arguments.
            GetProtectedString<BunnyArguments>(a => a.APIKey).
            Required();

        public override async Task<BunnyOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            return new BunnyOptions()
            {
                APIKey = await APIKey.Interactive(input).GetValue(),
            };
        }

        public override async Task<BunnyOptions?> Default()
        {
            return new BunnyOptions()
            {
                APIKey = await APIKey.GetValue(),
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(BunnyOptions options)
        {
            yield return (APIKey.Meta, options.APIKey);
        }
    }
}
