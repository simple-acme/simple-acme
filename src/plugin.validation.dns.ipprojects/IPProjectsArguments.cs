using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins;

/// <summary>
/// Command line arguments for unattended mode.
/// </summary>
public sealed class IPProjectsArguments : BaseArguments
{
    [CommandLine(Description = "API Access Key from Dashboard", Secret = true)]
    public string? ApiKey { get; set; }
}
