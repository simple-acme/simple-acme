using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.TargetPlugins
{
    internal class CsrArguments : BaseArguments
    {
        [CommandLine(Description = "Specify the location of a CSR file to make a certificate for")]
        public string? CsrFile { get; set; }

        [CommandLine(Description = "Specify the location of a script that will generate the CSR file on demand")]
        public string? CsrScript { get; set; }

        [CommandLine(Name = "csrarguments", Description = "Arguments passed to the CSR script")]
        public string? CsrScriptArguments { get; set; }

        [CommandLine(Description = "Specify the location of the private key corresponding to the CSR")]
        public string? PkFile { get; set; }
    }
}
