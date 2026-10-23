using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    public class SimplyArguments : BaseArguments
    {
        [CommandLine(Description = "Not used (the API key alone identifies the account).", Obsolete = true)]
        public string? Account { get; set; }

        [CommandLine(Description = "Simply API key.", Secret = true)]
        public string? ApiKey { get; set; }
    }
}