using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    /// <summary>
    /// Simply DNS validation
    /// </summary>
    internal class SimplyOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<SimplyOptions>
    {
        private ArgumentResult<ProtectedString?> ApiKey => arguments.
            GetProtectedString<SimplyArguments>(a => a.ApiKey).
            Required();

        public override async Task<SimplyOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            return new SimplyOptions()
            {
                ApiKey = await ApiKey.Interactive(input).GetValue()
            };
        }

        public override async Task<SimplyOptions?> Default()
        {
            return new SimplyOptions()
            {
                ApiKey = await ApiKey.GetValue()
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(SimplyOptions options)
        {
            yield return (ApiKey.Meta, options.ApiKey);
        }
    }
}
