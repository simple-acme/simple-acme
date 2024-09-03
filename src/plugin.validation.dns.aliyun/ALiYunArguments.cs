using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    public class ALiYunArguments : BaseArguments
    {
        [CommandLine(Description = "DNS Server Domain Name. Refer: https://api.aliyun.com/product/Alidns", Secret = false)]
        public string? ALiYunServer { get; set; } = "dns.aliyuncs.com";

        [CommandLine(Description = "API ID for ALiYun.", Secret = true)]
        public string? ALiYunApiID { get; set; }

        [CommandLine(Description = "API Secret for ALiYun.", Secret = true)]
        public string? ALiYunApiSecret { get; set; }
    }
}
