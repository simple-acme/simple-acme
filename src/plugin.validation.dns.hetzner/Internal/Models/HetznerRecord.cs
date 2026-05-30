namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns.Internal.Models;

internal sealed record HetznerRecord(string type, string name, string value, string zone_id, int ttl = 3600);