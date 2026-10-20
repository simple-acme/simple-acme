using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Services.Serialization;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    /// <summary>
    /// Generated code for (de)serializing DnsMintOptions.
    /// </summary>
    [JsonSerializable(typeof(DnsMintOptions))]
    internal partial class DnsMintJson : JsonSerializerContext
    {
        public DnsMintJson(WacsJsonPluginsOptionsFactory optionsFactory) : base(optionsFactory.Options) { }
    }

    internal class DnsMintOptions : ValidationPluginOptions
    {
        /// <summary>
        /// Encrypted in renewal.json.
        /// </summary>
        public ProtectedString? ApiKey { get; set; }
    }
}
