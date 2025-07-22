using PKISharp.WACS.Plugins.ValidationPlugins.Http;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Any
{
    internal class ManualOptions : HttpValidationOptions
    {
        public ManualOptions() => Path = "";
    }
}
