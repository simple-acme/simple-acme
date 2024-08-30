using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    public class CloudflareArguments : BaseArguments
    {
        [CommandLine(Description = "API Token for Cloudflare.", Secret = true)]
        public string? CloudflareApiToken { get; set; }
    }
}
