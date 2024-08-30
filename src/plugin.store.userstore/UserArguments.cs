using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    internal class UserArguments : BaseArguments
    {
        [CommandLine(Description = "While renewing, do not remove the previous certificate.")]
        public bool KeepExisting { get; set; }
    }
}
