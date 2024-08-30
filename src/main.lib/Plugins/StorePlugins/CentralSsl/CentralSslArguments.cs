using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    internal class CentralSslArguments : BaseArguments
    {
        [CommandLine(Description = "Location of the IIS Central Certificate Store.")]
        public string? CentralSslStore { get; set; }

        [CommandLine(Description = "Password to set for .pfx files exported to the IIS Central Certificate Store.", Secret = true)]
        public string? PfxPassword { get; set; }
    }
}
