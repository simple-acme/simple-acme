using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    public class NS1Arguments : BaseArguments
    {
        [CommandLine(Description = "NS1 API key.")]
        public string? ApiKey { get; set; }
    }
}
