using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    public class TencentOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<TencentOptions>
    {
        private ArgumentResult<ProtectedString?> ApiID => arguments.
            GetProtectedString<TencentArguments>(a => a.TencentApiID).
            Required();

        private ArgumentResult<ProtectedString?> ApiKey => arguments.
            GetProtectedString<TencentArguments>(a => a.TencentApiKey).
            Required();

        public override async Task<TencentOptions?> Aquire(IInputService inputService, RunLevel runLevel)
        {
            return new TencentOptions
            {
                ApiID = await ApiID.Interactive(inputService).WithLabel("Tencent API ID").GetValue(),
                ApiKey = await ApiKey.Interactive(inputService).WithLabel("Tencent API Key").GetValue(),
            };
        }

        public override async Task<TencentOptions?> Default()
        {
            return new TencentOptions
            {
                ApiID = await ApiID.GetValue(),
                ApiKey = await ApiKey.GetValue(),
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(TencentOptions options)
        {
            yield return (ApiID.Meta, options.ApiID);
            yield return (ApiKey.Meta, options.ApiKey);
        }
    }
}
