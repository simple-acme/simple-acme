using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Services.Serialization;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [JsonSerializable(typeof(Route53Options))]
    internal partial class Route53Json : JsonSerializerContext
    {
        public Route53Json(WacsJsonPluginsOptionsFactory optionsFactory) : base(optionsFactory.Options) { }
    }

    internal sealed class Route53Options : ValidationPluginOptions
    {
        /// <summary>
        /// Role on the local machine
        /// </summary>
        public string? IAMRole { get; set; }

        /// <summary>
        /// Role anywhere
        /// </summary>
        public string? ARNRole { get; set; }

        /// <summary>
        /// Basic authentication
        /// </summary>
        public string? AccessKeyId { get; set; }
        [JsonPropertyName("SecretAccessKeySafe")]
        public ProtectedString? SecretAccessKey { get; set; }
    }
}