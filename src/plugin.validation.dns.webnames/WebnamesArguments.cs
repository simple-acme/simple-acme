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

    [CommandLine(Description = "TTL in seconds to use when creating TXT record. Note that only the following values are supported: 300 (default), 900, 1800, 2700, 3600, 7200, 10800, 14400, 18000, 21600.", Default = "300")]
    public int? TTL { get; set; }
}
