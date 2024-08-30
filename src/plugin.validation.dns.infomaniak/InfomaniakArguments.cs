using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins;

public class InfomaniakArguments : BaseArguments
{
    [CommandLine(Description = "Infomaniak API token.", Secret = true)]
    public string? ApiToken { get; set; }
}