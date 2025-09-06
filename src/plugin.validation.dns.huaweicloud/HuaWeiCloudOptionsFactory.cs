using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    public class HuaWeiCloudOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<HuaWeiCloudOptions>
    {
        private ArgumentResult<string?> DnsRegion => arguments.GetString<HuaWeiCloudArguments>(a => a.HuaWeiCloudDnsRegion).Required();

        private ArgumentResult<ProtectedString?> KeyID => arguments.GetProtectedString<HuaWeiCloudArguments>(a => a.HuaWeiCloudKeyID).Required();

        private ArgumentResult<ProtectedString?> KeySecret => arguments.GetProtectedString<HuaWeiCloudArguments>(a => a.HuaWeiCloudKeySecret).Required();

        public override async Task<HuaWeiCloudOptions?> Aquire(IInputService inputService, RunLevel runLevel)
        {
            return new HuaWeiCloudOptions
            {
                DnsRegion = await DnsRegion.Interactive(inputService).WithLabel("HuaWeiCloud Dns Region").GetValue(),
                KeyID = await KeyID.Interactive(inputService).WithLabel("HuaWeiCloud AccessKey ID").GetValue(),
                KeySecret = await KeySecret.Interactive(inputService).WithLabel("HuaWeiCloud AccessKey Secret").GetValue(),
            };
        }

        public override async Task<HuaWeiCloudOptions?> Default()
        {
            return new HuaWeiCloudOptions
            {
                DnsRegion = await DnsRegion.GetValue(),
                KeyID = await KeyID.GetValue(),
                KeySecret = await KeySecret.GetValue(),
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(HuaWeiCloudOptions options)
        {
            yield return (DnsRegion.Meta, options.DnsRegion);
            yield return (KeyID.Meta, options.KeyID);
            yield return (KeySecret.Meta, options.KeySecret);
        }
    }
}
