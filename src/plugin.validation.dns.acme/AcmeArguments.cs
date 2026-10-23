using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    public class AcmeArguments : BaseArguments, INetworkCredentialArguments
    {
        [CommandLine(Description = "Root URI of the acme-dns service")]
        public string? AcmeDnsServer { get; set; }

        [CommandLine(Description = "Basic Authentication username for the acme-dns server")]
        public string? UserName { get; set; }

        [CommandLine(Description = "Basic Authentication password for the acme-dns server", Secret = true)]
        public string? Password { get; set; }
    }
}
