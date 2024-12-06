using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Services.Serialization;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [JsonSerializable(typeof(HuaWeiCloudOptions))]
    internal partial class HuaWeiCloudJson : JsonSerializerContext
    {
        public HuaWeiCloudJson(WacsJsonPluginsOptionsFactory optionsFactory) : base(optionsFactory.Options)
        {
        }
    }

    public class HuaWeiCloudOptions : ValidationPluginOptions
    {
        /// <summary>
        /// DnsRegion
        /// </summary>
        public string? DnsRegion { get; set; }

        /// <summary>
        /// KeyID
        /// </summary>
        public ProtectedString? KeyID { get; set; }

        /// <summary>
        /// KeySecret
        /// </summary>
        public ProtectedString? KeySecret { get; set; }
    }
}
