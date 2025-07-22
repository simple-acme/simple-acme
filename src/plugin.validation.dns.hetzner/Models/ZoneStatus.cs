using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns.Models;

[JsonConverter(typeof(JsonStringEnumConverter<ZoneStatus>))]
public enum ZoneStatus
{
    Verified,
    Failed,
    Pending
}