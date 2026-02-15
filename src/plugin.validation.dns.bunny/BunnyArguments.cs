
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    public sealed class BunnyArguments : BaseArguments
    {
        [CommandLine(Description = "APIKey for your Bunny Account.", Secret = true)]
        public string? APIKey { get; set; }
    }
}
