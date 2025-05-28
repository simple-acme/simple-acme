using PKISharp.WACS.Configuration.Settings.Store;
using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings
{
    public interface IProxySettings
    {        
        /// <summary>
        /// Password used to access the proxy server.
        /// </summary>
        string? Password { get; }

        /// <summary>
        /// Configures a proxy server to use for 
        /// communication with the ACME server. The 
        /// default setting uses the system proxy.
        /// Passing an empty string will bypass the 
        /// system proxy.
        /// </summary>
        string? Url { get; }

        /// <summary>
        /// Username used to access the proxy server.
        /// </summary>
        string? Username { get; }
    }

    internal class InheritProxySettings(params IEnumerable<ProxySettings?> chain) : InheritSettings<ProxySettings>(chain), IProxySettings
    {
        public string? Password => Get(x => x.Password);
        public string? Url => Get(x => x.Url);
        public string? Username => Get(x => x.Username);
    }

    internal class ProxySettings
    {
        public string? Url { get; set; }
        public string? Username { get; set; }
        public string? Password { get; set; }
    }
}