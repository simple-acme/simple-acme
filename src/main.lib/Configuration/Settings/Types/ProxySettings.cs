using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types
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
        public string? Username => Get(x => x.UserName);
    }

    internal class ProxySettings
    {
        [SettingsValue(
            SubType = "uri",
            Default = $"\"[system]\"",
            Description = "Configures a proxy server to use for communication with the ACME " +
            "server and other HTTP requests done by the program. " +
            "<div class=\"callout-block callout-block-success pb-1 mt-3\">" +
            "<div class=\"content\">" +
            "<table class=\"table table-bordered\">" +
            "<tr><th class=\"col-md-3\">Value</th><th>Meaning</th></tr>" +
            "<tr><td><code>\"[system]\"</code></td><td>Equivalent to <code>\"[wininet]\"</code></td></tr>" +
            "<tr><td><code>\"[wininet]\"</code></td><td>Auto-discover proxy using the legacy Windows Internet API.</td></tr>" +
            "<tr><td><code>\"[winhttp]\"</code></td><td>Auto-discover proxy using the modern Windows HTTP API. Honestly this should be the default, but isn't because of backwards compatibility.</td></tr>" +
            "<tr><td>Url</td><td>Explictly define a proxy url, e.g. <code>\"https://proxy.example.com:8080/\"</code>.</td></tr>" +
            "<tr><td>Emtpy</td><td>Attempt to bypass the system proxy</td></tr>" +
            "</table></div></div>")]
        public string? Url { get; set; }

        [SettingsValue(Description = "Username used to access the proxy server.")]
        public string? UserName { get; set; }

        [SettingsValue(SubType = "secret", Description = "Password used to access the proxy server.")]
        public string? Password { get; set; }
    }
}