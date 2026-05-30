
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    /// <summary>
    /// Command line arguments for unattended mode.
    /// In this case --dnsapikey.
    /// Where --dnsapikey is marked as Secret so 
    /// it will not be reflected in the logs.
    /// </summary>
    public sealed class ArsysArguments : BaseArguments
    {
        [CommandLine(Description = "Arsys DNS API Key", Secret = true)]
        public string? DNSApiKey { get; set; }
    }
}
