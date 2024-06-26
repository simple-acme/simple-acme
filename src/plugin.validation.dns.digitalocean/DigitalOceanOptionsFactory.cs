﻿using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    internal class DigitalOceanOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<DigitalOceanOptions>
    {
        private ArgumentResult<ProtectedString?> ApiKey => arguments.
            GetProtectedString<DigitalOceanArguments>(a => a.ApiToken).
            Required();

        public override async Task<DigitalOceanOptions?> Aquire(IInputService inputService, RunLevel runLevel)
        {
            return new DigitalOceanOptions
            {
                ApiToken = await ApiKey.Interactive(inputService).GetValue()
            };
        }

        public override async Task<DigitalOceanOptions?> Default()
        {
            return new DigitalOceanOptions
            {
                ApiToken = await ApiKey.GetValue()
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(DigitalOceanOptions options)
        {
            yield return (ApiKey.Meta, options.ApiToken);
        }
    }
}