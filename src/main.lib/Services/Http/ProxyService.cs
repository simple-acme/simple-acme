using System;
using System.Net;
using System.Net.Http;
using System.Runtime.Versioning;
using System.Security.Authentication;

namespace PKISharp.WACS.Services
{
    public partial class ProxyService(ILogService log, ISettingsService settings, SecretServiceManager secretService) : IProxyService
    {
        private IWebProxy? _proxy;

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
        public bool CustomProxy =>
            settings.Proxy.Url?.ToLower().Trim() switch
            {
                "[winhttp]" => false,
                "[wininet]" => false,
                "[system]" => false,
                "" => false,
                null => false,
                _ => true
            };

        public HttpMessageHandler GetHttpMessageHandler() => GetHttpMessageHandler(true);
        public HttpMessageHandler GetHttpMessageHandler(bool checkSsl = true)
        {
            var logger = new RequestLogger(log);
            if (OperatingSystem.IsWindows())
            {
                var winHandler = new WindowsHandler(logger)
                {
                    Proxy = GetWebProxy(),
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
                return winHandler;
            }
            else
            {
                var linuxHandler = new LinuxHandler(logger)
                {
                    Proxy = GetWebProxy(),
                    SslProtocols = SslProtocols
                }; 
                if (!checkSsl)
                {
                    linuxHandler.ServerCertificateCustomValidationCallback = (a, b, c, d) => true;
                }
                return linuxHandler;
            }
        }

        /// <summary>
        /// Get prepared HttpClient with correct system proxy settings
        /// </summary>
        /// <returns></returns>
        public HttpClient GetHttpClient(bool checkSsl = true)
        {
            var httpClientHandler = GetHttpMessageHandler(checkSsl);
            var httpClient = new HttpClient(httpClientHandler);
            httpClient.DefaultRequestHeaders.Add("User-Agent", $"simple-acme/{VersionService.SoftwareVersion} (+https://github.com/simple-acme/simple-acme)");
            return httpClient;
        }


        /// <summary>
        /// Get proxy server to use for web requests
        /// </summary>
        /// <returns></returns>
        
        public IWebProxy? GetWebProxy()
        {
            if (_proxy == null)
            {
                var proxy = CustomProxy ? new WebProxy(settings.Proxy.Url) : null;
                if (proxy != null)
                {
                    if (!string.IsNullOrWhiteSpace(settings.Proxy.Username))
                    {
                        var password = secretService.EvaluateSecret(settings.Proxy.Password);
                        proxy.Credentials = new NetworkCredential(settings.Proxy.Username, password);
                    }
                    log.Warning("Proxying via {proxy}:{port}", proxy.Address?.Host, proxy.Address?.Port);
                }
                _proxy = proxy;
            }
            return _proxy;
        }
    }
}
