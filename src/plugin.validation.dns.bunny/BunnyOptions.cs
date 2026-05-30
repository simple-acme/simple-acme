using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Services.Serialization;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    [JsonSerializable(typeof(BunnyOptions))]
    internal partial class BunnyJson : JsonSerializerContext
    {
        public BunnyJson(WacsJsonPluginsOptionsFactory optionsFactory) : base(optionsFactory.Options) { }
    }

    internal class BunnyOptions : ValidationPluginOptions
    {
        public ProtectedString? APIKey { get; set; }
    }
}
