using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    public class HetznerOptionsFactory : PluginOptionsFactory<HetznerOptions>
    {
        private readonly ArgumentsInputService _arguments;

        public HetznerOptionsFactory(ArgumentsInputService arguments) => _arguments = arguments;

        private ArgumentResult<ProtectedString?> ApiKey => _arguments
            .GetProtectedString<HetznerArguments>(a => a.HetznerApiToken)
            .Required();

        private ArgumentResult<bool?> UseHetznerCloud => _arguments
            .GetBool<HetznerArguments>(a => a.UseHetznerCloud)
            .WithDefault(false);

        private ArgumentResult<string?> ZoneId => _arguments
            .GetString<HetznerArguments>(a => a.HetznerZoneId)
            .DefaultAsNull();

        public override async Task<HetznerOptions?> Aquire(IInputService inputService, RunLevel runLevel)
            => new HetznerOptions
            {
                ApiToken = await ApiKey.Interactive(inputService).WithLabel("Hetzner API Token").GetValue(),
                ZoneId = await ZoneId.Interactive(inputService).WithLabel("Hetzner Zone Id").GetValue(),
                UseHetznerCloud = await UseHetznerCloud.Interactive(inputService).WithLabel("Use Hetzner Cloud API").GetValue() ?? true
            };

        public override async Task<HetznerOptions?> Default()
            => new HetznerOptions
            {
                ApiToken = await ApiKey.GetValue(),
                ZoneId = await ZoneId.GetValue(),
                UseHetznerCloud = await UseHetznerCloud.GetValue() ?? true
            };

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(HetznerOptions options)
        {
            yield return (ApiKey.Meta, options.ApiToken);
            yield return (ZoneId.Meta, options.ZoneId);
            yield return (UseHetznerCloud.Meta, options.UseHetznerCloud);
        }
    }
}
