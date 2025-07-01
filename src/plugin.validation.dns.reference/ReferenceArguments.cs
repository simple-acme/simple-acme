
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    /// <summary>
    /// Command line arguments for unattended mode.
    /// In this case --clientid and --clientsecret.
    /// Where --clientsecret is marked as Secret so 
    /// it will not be reflected in the logs.
    /// </summary>
    public sealed class ReferenceArguments : BaseArguments
    {
        [CommandLine(Description = "Reference ClientId")]
        public string? ClientId { get; set; }

        [CommandLine(Description = "Reference ClientSecret", Secret = true)]
        public string? ClientSecret { get; set; }
    }
}
