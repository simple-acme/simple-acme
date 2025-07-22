namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns.Models;

public sealed record Record(string type, string name, string value, string zone_id, int ttl = 3600);