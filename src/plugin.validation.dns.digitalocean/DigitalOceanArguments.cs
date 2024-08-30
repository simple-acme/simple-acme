using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    public class DigitalOceanArguments : BaseArguments
    {
        [CommandLine(Name = "digitaloceanapitoken", Description = "The API token to authenticate against the DigitalOcean API.", Secret = true)]
        public string? ApiToken { get; set; }
    }
}