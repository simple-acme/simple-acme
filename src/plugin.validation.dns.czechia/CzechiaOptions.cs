using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Services.Serialization;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [JsonSerializable(typeof(CzechiaOptions))]
    internal partial class CzechiaJson : JsonSerializerContext
    {
        public CzechiaJson(WacsJsonPluginsOptionsFactory optionsFactory) : base(optionsFactory.Options) { }
    }

    public class CzechiaOptions : ValidationPluginOptions
    {
        public string? ApiBaseUri { get; set; }
        public ProtectedString? ApiToken { get; set; }
        public string? ZoneName { get; set; }
        public int? Ttl { get; set; }
    }
}
