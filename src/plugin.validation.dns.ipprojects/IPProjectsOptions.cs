using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Services.Serialization;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.ValidationPlugins;

[JsonSerializable(typeof(IPProjectsOptions))]
internal partial class IPProjectsJson : JsonSerializerContext
{
    public IPProjectsJson(WacsJsonPluginsOptionsFactory optionsFactory) : base(optionsFactory.Options)
    {
    }
}

internal class IPProjectsOptions : ValidationPluginOptions
{
    public ProtectedString? ApiKey { get; set; }
}
