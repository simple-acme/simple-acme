using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins;

/// <summary>
/// Command line arguments for unattended mode.
/// In this case --APIUsername and --APIKey.
/// Where --APIKey is marked as Secret so 
/// it will not be reflected in the logs.
/// </summary>
public sealed class WebnamesArguments : BaseArguments
{
    [CommandLine(Description = "Webnames API Username")]
    public string? APIUsername { get; set; }

    [CommandLine(Description = "Webnames API Key", Secret = true)]
    public string? APIKey { get; set; }

    [CommandLine(Description = "Webnames API Base URL Override (leave blank except for testing/debugging)", Default = null)]
    public string? APIOverrideBaseURL { get; set; }
}
