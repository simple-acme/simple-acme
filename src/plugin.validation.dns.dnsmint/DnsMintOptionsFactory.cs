using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    internal class DnsMintOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<DnsMintOptions>
    {
        private ArgumentResult<ProtectedString?> ApiKey => arguments.
            GetProtectedString<DnsMintArguments>(a => a.ApiKey).
            Required();

        public override async Task<DnsMintOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            return new DnsMintOptions()
            {
                ApiKey = await ApiKey.Interactive(input).GetValue()
            };
        }

        public override async Task<DnsMintOptions?> Default()
        {
            return new DnsMintOptions()
            {
                ApiKey = await ApiKey.GetValue()
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(DnsMintOptions options)
        {
            yield return (ApiKey.Meta, options.ApiKey);
        }
    }
}
