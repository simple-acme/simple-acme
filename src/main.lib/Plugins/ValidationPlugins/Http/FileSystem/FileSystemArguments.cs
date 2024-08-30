using PKISharp.WACS.Configuration;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    internal class FileSystemArguments : HttpValidationArguments
    {
        [CommandLine(Description = "Specify IIS site to use for handling validation requests. This will be used to choose the web root path.")]
        public long? ValidationSiteId { get; set; }
    }
}
