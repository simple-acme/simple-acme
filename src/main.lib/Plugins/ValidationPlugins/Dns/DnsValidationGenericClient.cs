using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Services;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{

    public abstract class DnsValidation<TPlugin, TClient>(LookupClientProvider dnsClient, ILogService log, ISettings settings, IProxyService proxy) :
        DnsValidation<TPlugin>(dnsClient, log, settings), IDisposable
        where TClient : class
    {
        protected IProxyService _proxy = proxy;

        private HttpClient? _httpClient = default;
        protected async Task<HttpClient> GetHttpClient()
        {
            if (_httpClient == default)
            {
                _httpClient = await _proxy.GetHttpClient();
            }
            return _httpClient;
        }

        private TClient? _cachedClient = default;
        protected async Task<TClient> GetClient()
        {
            if (_cachedClient == default)
            {
                _log.Debug("Client of type {x} created", typeof(TClient).Name);
                var httpClient = await GetHttpClient();
                _cachedClient = await CreateClient(httpClient);
            }
            return _cachedClient;
        }
        protected internal abstract Task<TClient> CreateClient(HttpClient httpClient);

        public void Dispose()
        {
            if (_cachedClient is IDisposable disposable)
            {
                _log.Debug("Client of type {x} disposed", typeof(TClient).Name);
                disposable?.Dispose();
            }
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
