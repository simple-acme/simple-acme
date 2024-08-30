using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    public sealed class TransIpArguments : BaseArguments
    {
        [CommandLine(Name = "transip-login", Description = "Login name at TransIp.")]
        public string? Login { get; set; }

        [CommandLine(Name = "transip-privatekey", Description = "Private key generated in the control panel (replace enters by spaces and use quotes).", Secret = true)]
        public string? PrivateKey { get; set; }

        [CommandLine(Name = "transip-privatekeyfile", Description = "Private key generated in the control panel (saved to a file on disk).")]
        public string? PrivateKeyFile { get; set; }
    }
}