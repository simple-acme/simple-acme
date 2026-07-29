using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    public sealed class Level27Arguments : BaseArguments
    {
        [CommandLine(Description = "API key for your Level27 account. Get one from the Level27 control panel (https://app.level27.eu/account/profile/security).", Secret = true)]
        public string? ApiKey { get; set; }

        [CommandLine(Description = "Optional. Level27 API base URL. Defaults to https://api.level27.eu/v1.")]
        public string? ApiBaseUrl { get; set; }
    }
}
