using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    /// <summary>
    /// Azure DNS validation
    /// </summary>
    internal class DreamhostOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<DreamhostOptions>
    {
        private ArgumentResult<ProtectedString?> ApiKey => arguments.
            GetProtectedString<DreamhostArguments>(a => a.ApiKey).
            Required();

        public override async Task<DreamhostOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            return new DreamhostOptions()
            {
                ApiKey = await ApiKey.Interactive(input).GetValue()
            };
        }

        public override async Task<DreamhostOptions?> Default()
        {
            return new DreamhostOptions()
            {
                ApiKey = await ApiKey.GetValue()
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(DreamhostOptions options)
        {
            yield return (ApiKey.Meta, options.ApiKey);
        }

    }
}
