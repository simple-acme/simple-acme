namespace PKISharp.WACS.Configuration.Settings
{
    public class ProxySettings
    {
        /// <summary>
        /// Configures a proxy server to use for 
        /// communication with the ACME server. The 
        /// default setting uses the system proxy.
        /// Passing an empty string will bypass the 
        /// system proxy.
        /// </summary>
        public string? Url { get; set; }
        /// <summary>
        /// Username used to access the proxy server.
        /// </summary>
        public string? Username { get; set; }
        /// <summary>
        /// Password used to access the proxy server.
        /// </summary>
        public string? Password { get; set; }
    }
}