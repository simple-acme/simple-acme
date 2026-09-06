using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Services.Serialization;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    [JsonSerializable(typeof(Level27Options))]
    internal partial class Level27Json : JsonSerializerContext
    {
        public Level27Json(WacsJsonPluginsOptionsFactory optionsFactory) : base(optionsFactory.Options) { }
    }

    internal class Level27Options : ValidationPluginOptions
    {
        public ProtectedString? ApiKey { get; set; }

        /// <summary>
        /// Optional override for the Level27 API base URL. When null the
        /// default (https://api.level27.eu/v1) is used.
        /// </summary>
        public string? ApiBaseUrl { get; set; }
    }
}
