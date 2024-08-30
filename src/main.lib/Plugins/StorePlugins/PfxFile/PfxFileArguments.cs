using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    internal class PfxFileArguments : BaseArguments
    {
        [CommandLine(Description = "Path to write the .pfx file to.")]
        public string? PfxFilePath { get; set; }

        [CommandLine(Description = "Prefix to use for the .pfx file, defaults to the common name.")]
        public string? PfxFileName { get; set; }

        [CommandLine(Description = "Password to set for .pfx file exported to the folder.", Secret = true)]
        public string? PfxPassword { get; set; }
    }
}
