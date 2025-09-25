using PKISharp.WACS.Services;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    internal class HttpListenerFactory(ILogService log) : ISelfHosterFactory
    {
        public ISelfHoster Create(ISelfHosterOptions options) => new HttpListenerWrapper(options, log);
    }
}
