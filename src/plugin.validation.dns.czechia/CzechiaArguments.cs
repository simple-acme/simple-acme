using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    public sealed class CzechiaArguments : BaseArguments
    {
        [CommandLine(Description = "Czechia API base uri (default: https://api.czechia.com/api)")]
        public string? ApiBaseUri { get; set; }

        [CommandLine(Description = "Czechia API token (AuthorizationToken header)", Secret = true)]
        public string? ApiToken { get; set; }

        [CommandLine(Description = "DNS zone name used in the endpoint URL, e.g. example.com")]
        public string? ZoneName { get; set; }

        [CommandLine(Description = "TXT record TTL (default: 3600)")]
        public int? Ttl { get; set; }
    }
}
