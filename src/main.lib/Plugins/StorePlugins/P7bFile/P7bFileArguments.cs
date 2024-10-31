using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    internal class P7bFileArguments : BaseArguments
    {
        [CommandLine(Description = "Path to write the .p7b file to.")]
        public string? P7bFilePath { get; set; }

        [CommandLine(Description = "Prefix to use for the .p7b file, defaults to the common name.")]
        public string? P7bFileName { get; set; }
    }
}
