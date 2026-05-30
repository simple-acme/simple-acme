using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Services.Serialization;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    /// <summary>
    /// Generated code for (de)serializing ArsysOptions.
    /// </summary>
    [JsonSerializable(typeof(ArsysOptions))]
    internal partial class ArsysJson : JsonSerializerContext
    {
        public ArsysJson(WacsJsonPluginsOptionsFactory optionsFactory) : base(optionsFactory.Options) { }
    }

    /// <summary>
    /// Options as stored in the JSON. Use primitive types where possible
    /// and defensively check for nulls (make everything nullable) for 
    /// optimal backwards/forwards compatibility.
    /// </summary>
    internal class ArsysOptions : ValidationPluginOptions
    {
        /// <summary>
        /// This is a protected string, which will be encrypted in the renewal.json file.
        /// </summary>
        public ProtectedString? DNSApiKey { get; set; }
    }
}
