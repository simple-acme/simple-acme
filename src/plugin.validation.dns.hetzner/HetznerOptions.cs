using System.Text.Json.Serialization;

using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Services.Serialization;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [JsonSerializable(typeof(HetznerOptions))]
    internal partial class HetznerJson : JsonSerializerContext
    {
        public HetznerJson(WacsJsonPluginsOptionsFactory optionsFactory) : base(optionsFactory.Options) { }
    }

    public class HetznerOptions : ValidationPluginOptions
    {
        public ProtectedString? ApiToken { get; set; }

        public string? ZoneId { get; set; }

        public bool UseHetznerCloud { get; set; }
    }
}