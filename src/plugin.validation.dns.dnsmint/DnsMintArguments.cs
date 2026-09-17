using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    /// <summary>
    /// Command line arguments for unattended mode. An API key is the whole
    /// credential, and it is marked Secret so it stays out of the logs.
    /// </summary>
    public sealed class DnsMintArguments : BaseArguments
    {
        [CommandLine(Description = "DNSMint API key, carrying the dns01:write scope.", Secret = true)]
        public string? ApiKey { get; set; }
    }
}
