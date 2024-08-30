using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    public class DnsMadeEasyArguments : BaseArguments
    {
        [CommandLine(Description = "DnsMadeEasy API key.")]
        public string? ApiKey { get; set; }

        [CommandLine(Description = "DnsMadeEasy API secret.", Secret = true)]
        public string? ApiSecret { get; set; }
    }
}