using PKISharp.WACS.Configuration.Settings.Types.Installation;
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Configuration.Settings.Types
{
    public interface IInstallationSettings
    {
        /// <summary>
        /// Default plugin(s) to select 
        /// </summary>
        string? DefaultInstallation { get; }
        IIISSettings? IIS { get; }
    }

    internal class InheritInstallationSettings(params IEnumerable<InstallationSettings?> chain) : InheritSettings<InstallationSettings>(chain), IInstallationSettings
    {
        public string? DefaultInstallation => Get(x => x.DefaultInstallation);
        public IIISSettings? IIS => new InheritIISSettings(Chain.Select(c => c?.IIS));
    }

    public class InstallationSettings
    {
        [SettingsValue(
            Description = "Default installation plugin(s).",
            Tip = "This may be a comma separated value for multiple default installation plugins.",
            NullBehaviour = "equivalent to <code>\"none\"</code> for most unattended usage (unless <code>‑‑source iis</code> is provided) and <code>\"iis\"</code> for interactive mode")]
        public string? DefaultInstallation { get; set; }
        public IISSettings? IIS { get; set; }
    }
}