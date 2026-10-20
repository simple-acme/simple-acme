using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types.Validation
{
    /// <summary>
    /// Settings for self-hosting validation
    /// </summary>
    public interface IHttpSelfHostingSettings
    {
        int? Port { get; }
    }

    internal class InheritHttpSelfHostingSettings(params IEnumerable<HttpSelfHostingSettings?> chain) : InheritSettings<HttpSelfHostingSettings>(chain), IHttpSelfHostingSettings
    {
        public int? Port => Get(x => x.Port);
    }

    public class HttpSelfHostingSettings
    {
        [SettingsValue(
            Description = "Port to listen to. Default (null) results in using port 80 for HTTP and 443 for HTTPS.",
            Warning = "The program is not able to influence the validation procedure. Using non-default ports is only possible if you control the server and/or use port forwarding.")]
        public int? Port { get; set; }
    }
}