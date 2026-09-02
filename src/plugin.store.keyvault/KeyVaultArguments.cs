using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Azure.Common;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    public class KeyVaultArguments : AzureArgumentsCommon
    {
        [CommandLine(Description = "The name of the vault")]
        public string? VaultName { get; set; }

        [CommandLine(Description = "The name for the certificate. Defaults to the friendly name.")]
        public string? CertificateName { get; set; }

        [CommandLine(Description = "The password for the certificate", Secret = true)]
        public string? CertificatePassword { get; set; }
    }
}