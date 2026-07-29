using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    internal class Level27OptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<Level27Options>
    {
        private ArgumentResult<ProtectedString?> ApiKey => arguments.
            GetProtectedString<Level27Arguments>(a => a.ApiKey).
            Required();

        private ArgumentResult<string?> ApiBaseUrl => arguments.
            GetString<Level27Arguments>(a => a.ApiBaseUrl);

        public override async Task<Level27Options?> Aquire(IInputService input, RunLevel runLevel)
        {
            return new Level27Options()
            {
                ApiKey = await ApiKey.Interactive(input).GetValue(),
                ApiBaseUrl = await ApiBaseUrl.GetValue(),
            };
        }

        public override async Task<Level27Options?> Default()
        {
            return new Level27Options()
            {
                ApiKey = await ApiKey.GetValue(),
                ApiBaseUrl = await ApiBaseUrl.GetValue(),
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(Level27Options options)
        {
            yield return (ApiKey.Meta, options.ApiKey);
            yield return (ApiBaseUrl.Meta, options.ApiBaseUrl);
        }
    }
}
