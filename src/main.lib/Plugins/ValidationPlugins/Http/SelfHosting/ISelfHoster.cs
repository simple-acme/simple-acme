using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    public interface ISelfHoster
    {
        public bool Started { get; }
        public ConcurrentDictionary<string, string> Challenges { get; }
        public int Port { get; }
        public Task Start();
        public Task Stop();
    }

    public interface ISelfHosterOptions
    {
        int? Port { get; }
        bool? Https { get; }
    }

    public interface ISelfHosterFactory
    {
        ISelfHoster Create(ISelfHosterOptions options);
    }
}
