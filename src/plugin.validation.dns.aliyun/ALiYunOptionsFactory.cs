﻿using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    public class ALiYunOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<ALiYunOptions>
    {
        private ArgumentResult<string?> ApiServer => arguments.GetString<ALiYunArguments>(a => a.ALiYunServer).Required();

        private ArgumentResult<ProtectedString?> ApiID => arguments.GetProtectedString<ALiYunArguments>(a => a.ALiYunApiID).Required();

        private ArgumentResult<ProtectedString?> ApiSecret => arguments.GetProtectedString<ALiYunArguments>(a => a.ALiYunApiSecret).Required();

        public override async Task<ALiYunOptions?> Aquire(IInputService inputService, RunLevel runLevel)
        {
            return new ALiYunOptions
            {
                ApiServer = await ApiServer.Interactive(inputService).WithLabel("ALiYun Domain Server").GetValue(),
                ApiID = await ApiID.Interactive(inputService).WithLabel("ALiYun AccessKey ID").GetValue(),
                ApiSecret = await ApiSecret.Interactive(inputService).WithLabel("ALiYun AccessKey Secret").GetValue(),
            };
        }

        public override async Task<ALiYunOptions?> Default()
        {
            return new ALiYunOptions
            {
                ApiServer = await ApiServer.GetValue(),
                ApiID = await ApiID.GetValue(),
                ApiSecret = await ApiSecret.GetValue(),
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(ALiYunOptions options)
        {
            yield return (ApiServer.Meta, options.ApiServer);
            yield return (ApiID.Meta, options.ApiID);
            yield return (ApiSecret.Meta, options.ApiSecret);
        }
    }
}
