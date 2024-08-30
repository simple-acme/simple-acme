using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.TargetPlugins
{
    internal class CsrArguments : BaseArguments
    {
        [CommandLine(Description = "Specify the location of a CSR file to make a certificate for")]
        public string? CsrFile { get; set; }

        [CommandLine(Description = "Specify the location of the private key corresponding to the CSR")]
        public string? PkFile { get; set; }
    }
}
