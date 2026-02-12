using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    internal class QiNiuArguments : BaseArguments
    {
        [CommandLine(Description = "Qiniu Cloud Server https://api.qiniu.com")]
        public string? QiNiuServer { get; set; } = "https://api.qiniu.com";

        [CommandLine(Description = "Qiniu Cloud AccessKey", Secret = true)]
        public string? AccessKey { get; set; }

        [CommandLine(Description = "Qiniu Cloud SecretKey", Secret = true)]
        public string? SecretKey { get; set; }

        [CommandLine(Description = "Password to set for the private key .pem file.", Secret = true)]
        public string? Password { get; set; }
    }
}
