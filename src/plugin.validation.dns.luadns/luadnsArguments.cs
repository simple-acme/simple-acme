using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    public sealed class LuaDnsArguments : BaseArguments
    {
        [CommandLine(Description = "LuaDNS account username (email address).")]
        public string? LuaDnsUsername { get; set; }

        [CommandLine(Description = "LuaDNS API key.", Secret = true)]
        public string? LuaDnsAPIKey { get; set; }
    }
}