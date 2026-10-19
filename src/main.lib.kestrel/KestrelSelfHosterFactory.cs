using PKISharp.WACS.Services;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    public class KestrelSelfHosterFactory(ILogService log) : ISelfHosterFactory
    {
        public ISelfHoster Create(ISelfHosterOptions options, ISettings settings)
        {
            return new KestrelSelfHoster(options, settings, log);
        }
    }
}
