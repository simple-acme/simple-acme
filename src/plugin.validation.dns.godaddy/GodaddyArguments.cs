using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    public class GodaddyArguments : BaseArguments
    {
        [CommandLine(Description = "GoDaddy API key.")]
        public string? ApiKey { get; set; }

        [CommandLine(Description = "GoDaddy API secret.", Secret = true)]
        public string? ApiSecret { get; set; }
    }
}