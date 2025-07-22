using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Services;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    public class HttpValidationParameters(
        ILogService log,
        IInputService input,
        ISettings settings,
        IProxyService proxy,
        Renewal renewal,
        RunLevel runLevel)
    {
        public ISettings Settings { get; private set; } = settings;
        public Renewal Renewal { get; private set; } = renewal;
        public RunLevel RunLevel { get; private set; } = runLevel;
        public ILogService LogService { get; private set; } = log;
        public IInputService InputService { get; private set; } = input;
        public IProxyService ProxyService { get; private set; } = proxy;
    }
}
