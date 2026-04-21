using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns.Internal.Models.HetznerCloud;

internal sealed class ZoneResponse
{
    [JsonPropertyName("zone")]
    public required Zone Zone { get; init; }
}