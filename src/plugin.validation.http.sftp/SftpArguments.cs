using PKISharp.WACS.Configuration;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    public class SftpArguments : HttpValidationArguments, INetworkCredentialArguments
    {
        [CommandLine(Description = "Username for remote server")]
        public string? UserName { get; set; }

        [CommandLine(Description = "Password for remote server", Secret = true)]
        public string? Password { get; set; }
    }
}
