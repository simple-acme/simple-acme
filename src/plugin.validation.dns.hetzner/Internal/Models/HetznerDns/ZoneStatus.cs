using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns.Internal.Models.HetznerDns;

[JsonConverter(typeof(JsonStringEnumConverter<ZoneStatus>))]
internal enum ZoneStatus
{
    Verified,
    Failed,
    Pending
}