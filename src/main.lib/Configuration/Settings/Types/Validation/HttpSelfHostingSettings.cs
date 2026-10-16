using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types.Validation
{
    /// <summary>
    /// Settings for self-hosting validation
    /// </summary>
    public interface IHttpSelfHostingSettings
    {
        int Port { get; }
    }

    internal class InheritHttpSelfHostingSettings(params IEnumerable<HttpSelfHostingSettings?> chain) : InheritSettings<HttpSelfHostingSettings>(chain), IHttpSelfHostingSettings
    {
        public int Port => Get(x => x.Port) ?? 80;
    }

    public class HttpSelfHostingSettings
    {
        [SettingsValue(
            Default = "80",
            Description = "Port to listen to.",
            Warning = "The program is not able to influence the validation procedure. Use this only if you control the server and/or use port forwarding.")]
        public int? Port { get; set; }
    }
}