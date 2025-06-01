using System;
using System.Net;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    public partial class ProxyService(ILogService log, ISettings settings, SecretServiceManager secretService) : IProxyService
    {
        private IWebProxy? _proxy;
        private bool _enabled = true;
        public SslProtocols SslProtocols { get; set; } = SslProtocols.None;

        /// <summary>
        /// Is the user requesting the system proxy
        /// </summary>
        [SupportedOSPlatform("windows")]
        public WindowsProxyUsePolicy ProxyType => 
            settings.Proxy.Url?.ToLower().Trim() switch
            {
                "[winhttp]" => WindowsProxyUsePolicy.UseWinHttpProxy,
                "[wininet]" => WindowsProxyUsePolicy.UseWinInetProxy,
                "[system]" => WindowsProxyUsePolicy.UseWinInetProxy,
                "" => WindowsProxyUsePolicy.DoNotUseProxy,
                null => WindowsProxyUsePolicy.DoNotUseProxy,
                _ => WindowsProxyUsePolicy.UseCustomProxy
        };

        /// <summary>
        /// Is the user requesting the system proxy
        /// </summary>
        private bool CustomProxy =>
            settings.Proxy.Url?.ToLower().Trim() switch
            {
                "[winhttp]" => false,
                "[wininet]" => false,
                "[system]" => false,
                "" => false,
                null => false,
                _ => true
            };

        public async Task<HttpMessageHandler> GetHttpMessageHandler() => await GetHttpMessageHandler(true);
        private async Task<HttpMessageHandler> GetHttpMessageHandler(bool checkSsl = true)
        {
            var logger = new RequestLogger(log);
            var handler = default(HttpMessageHandler);
            if (OperatingSystem.IsWindows() && _enabled)
            {
                var winHandler = new WindowsHandler(logger)
                {
                    Proxy = await GetWebProxy(),
                    SslProtocols = SslProtocols,
                };
                if (!checkSsl)
                {
                    winHandler.ServerCertificateValidationCallback = (a, b, c, d) => true;
                }
                winHandler.WindowsProxyUsePolicy = ProxyType;
                if (ProxyType == WindowsProxyUsePolicy.UseWinInetProxy)
                {
                    winHandler.DefaultProxyCredentials = CredentialCache.DefaultCredentials;
                }
                handler = winHandler;
            }
            else
            {
                var basicHandler = new BasicHandler(logger)
                {
                    Proxy = await GetWebProxy(),
                    SslProtocols = SslProtocols
                }; 
                if (!checkSsl)
                {
                    basicHandler.ServerCertificateCustomValidationCallback = (a, b, c, d) => true;
                }
                handler = basicHandler;
            }
            return handler;
        }

        /// <summary>
        /// Get prepared HttpClient with correct system proxy settings
        /// </summary>
        /// <returns></returns>
        public async Task<HttpClient> GetHttpClient(bool checkSsl = true)
        {
            var httpClientHandler = await GetHttpMessageHandler(checkSsl);
            var httpClient = new HttpClient(httpClientHandler);
            httpClient.DefaultRequestHeaders.Add("User-Agent", $"simple-acme/{VersionService.SoftwareVersion} (+https://github.com/simple-acme/simple-acme)");
            return httpClient;
        }


        /// <summary>
        /// Get proxy server to use for web requests
        /// </summary>
        /// <returns></returns>
        
        public async Task<IWebProxy?> GetWebProxy()
        {
            if (!_enabled)
            {
                return null;
            }
            if (_proxy == null)
            {
                var proxy = CustomProxy ? new WebProxy(settings.Proxy.Url) : null;
                if (proxy != null)
                {
                    if (!string.IsNullOrWhiteSpace(settings.Proxy.Username))
                    {
                        var password = await secretService.EvaluateSecret(settings.Proxy.Password);
                        proxy.Credentials = new NetworkCredential(settings.Proxy.Username, password);
                    }
                    log.Warning("Proxying via {proxy}:{port}", proxy.Address?.Host, proxy.Address?.Port);
                }
                _proxy = proxy;
            }
            return _proxy;
        }

        /// <summary>
        /// Disable proxy detection and (on Windows) fallback to basic HttpMessageHandler 
        /// instead of the WinHttpHandler, which sometimes fails especially in AWS VMs
        /// for some unknown reason.
        /// </summary>
        public void Disable()
        {
            _enabled = false;
            _proxy = null;
        }
    }
}
