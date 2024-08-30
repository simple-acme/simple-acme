using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.CsrPlugins
{
    public abstract class CsrArguments : BaseArguments
    {
        [CommandLine(Name = "ocsp-must-staple", Description = "Enable OCSP Must Staple extension on certificate.")]
        public bool OcspMustStaple { get; set; }

        [CommandLine(Name = "reuse-privatekey", Description = "Reuse the same private key for each renewal.")]
        public bool ReusePrivateKey { get; set; }
    }
}
