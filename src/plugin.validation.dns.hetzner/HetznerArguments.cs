using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    public class HetznerArguments : BaseArguments
    {
        [CommandLine(Description = "API Token for Hetzner.", Secret = true)]
        public string? HetznerApiToken { get; set; }

        [CommandLine(Description = "OPTIONAL: ID of zone the record is associated with.")]
        public string? HetznerZoneId { get; set; }

        [CommandLine(Description = "Use the Hetzner Cloud API. Obsolete - Do remove the usage of this argument. This dns validation plugin only supports the current Hetzner Cloud DNS API.", Obsolete = true)]
        public bool UseHetznerCloud { get; set; }
    }
}
