using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    public class TencentArguments : BaseArguments
    {
        [CommandLine(Description = "API ID for Tencent.", Secret = true)]
        public string? TencentApiID { get; set; }

        [CommandLine(Description = "API Key for Tencent.", Secret = true)]
        public string? TencentApiKey { get; set; }
    }
}
