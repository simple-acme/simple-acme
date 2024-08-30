
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    public sealed class DomeneshopArguments : BaseArguments
    {
        [CommandLine(Description = "Domeneshop ClientID (token).")]
        public string? ClientId { get; set; }

        [CommandLine(Description = "Domeneshop Client Secret.")]
        public string? ClientSecret { get; set; }
    }
}
