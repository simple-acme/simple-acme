using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types.Validation
{
    /// <summary>
    /// Settings for self-hosting validation
    /// </summary>
    public interface ITlsSelfHostingSettings
    {
        int? Port { get; }
    }

    internal class InheritTlsSelfHostingSettings(params IEnumerable<TlsSelfHostingSettings?> chain) : InheritSettings<TlsSelfHostingSettings>(chain), ITlsSelfHostingSettings
    {
        public int? Port => Get(x => x.Port);
    }

    public class TlsSelfHostingSettings
    {
        [SettingsValue(
            Default = "443",
            Description = "Port to listen to.",
            Warning = "The program is not able to influence the validation procedure. Use this only if you control the server and/or use port forwarding.")]
        public int? Port { get; set; }
    }
}