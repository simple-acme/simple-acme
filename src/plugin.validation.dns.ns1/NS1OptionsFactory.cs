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
    internal class NS1OptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<NS1Options>
    {
        private ArgumentResult<ProtectedString?> ApiKey => arguments.
            GetProtectedString<NS1Arguments>(a => a.ApiKey).
            Required();

        public override async Task<NS1Options?> Aquire(IInputService input, RunLevel runLevel)
        {
            return new NS1Options()
            {
                ApiKey = await ApiKey.Interactive(input).WithLabel("API key").GetValue(),
            };
        }

        public override async Task<NS1Options?> Default()
        {
            return new NS1Options()
            {
                ApiKey = await ApiKey.GetValue(),
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(NS1Options options)
        {
            yield return (ApiKey.Meta, options.ApiKey);
        }
    }
}
