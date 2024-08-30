using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    public class LinodeArguments : BaseArguments
    {
        [CommandLine(Description = "Linode Personal Access Token.", Secret = true)]
        public string? ApiToken { get; set; }
    }
}
