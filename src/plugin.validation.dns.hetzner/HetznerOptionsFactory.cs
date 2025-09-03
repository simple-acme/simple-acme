﻿using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    public class HetznerOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<HetznerOptions>
    {
        private ArgumentResult<ProtectedString?> ApiKey => arguments
            .GetProtectedString<HetznerArguments>(a => a.HetznerApiToken)
            .Required();

        private ArgumentResult<string?> ZoneId => arguments
            .GetString<HetznerArguments>(a => a.HetznerZoneId)
            .DefaultAsNull();

        public override async Task<HetznerOptions?> Aquire(IInputService inputService, RunLevel runLevel)
        {
            return new HetznerOptions
            {
                ApiToken = await ApiKey.Interactive(inputService).WithLabel("Hetzner API Token").GetValue(),
                ZoneId = await ZoneId.Interactive(inputService).WithLabel("Hetzner Zone Id").GetValue()
            };
        }

        public override async Task<HetznerOptions?> Default()
        {
            return new HetznerOptions
            {
                ApiToken = await ApiKey.GetValue(),
                ZoneId = await ZoneId.GetValue()
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(HetznerOptions options)
        {
            yield return (ApiKey.Meta, options.ApiToken);
            yield return (ZoneId.Meta, options.ZoneId);
        }
    }
}
