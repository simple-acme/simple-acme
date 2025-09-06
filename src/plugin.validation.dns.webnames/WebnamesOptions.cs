using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Services.Serialization;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.ValidationPlugins;

/// <summary>
/// Generated code for (de)serializing WebnamesOptions.
/// </summary>
[JsonSerializable(typeof(WebnamesOptions))]
internal partial class WebnamesJson : JsonSerializerContext
{
    public WebnamesJson(WacsJsonPluginsOptionsFactory optionsFactory) : base(optionsFactory.Options) { }
}

/// <summary>
/// Options as stored in the JSON. Use primitive types where possible
/// and defensively check for nulls (make everything nullable) for 
/// optimal backwards/forwards compatibility.
/// </summary>
internal class WebnamesOptions : ValidationPluginOptions
{
    public string? APIUsername { get; set; }

    /// <summary>
    /// This is a protected string, which will be encrypted in the renewal.json file.
    /// </summary>
    public ProtectedString? APIKey { get; set; }

    public string? APIOverrideBaseURL { get; set; }
}
