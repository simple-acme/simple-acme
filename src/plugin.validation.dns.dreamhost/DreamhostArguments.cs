using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    public class DreamhostArguments: BaseArguments
    {
        [CommandLine(Description = "Dreamhost API key.", Secret = true)]
        public string? ApiKey { get; set; }
    }
}