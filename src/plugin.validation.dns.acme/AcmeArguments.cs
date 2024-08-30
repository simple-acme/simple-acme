using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    public class AcmeArguments : BaseArguments
    {
        [CommandLine(Description = "Root URI of the acme-dns service")]
        public string? AcmeDnsServer { get; set; }
    }
}
