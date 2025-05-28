using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types
{
    public interface IInstallationSettings
    {
        /// <summary>
        /// Default plugin(s) to select 
        /// </summary>
        string? DefaultInstallation { get; }
    }

    internal class InheritInstallationSettings(params IEnumerable<InstallationSettings?> chain) : InheritSettings<InstallationSettings>(chain), IInstallationSettings
    {
        public string? DefaultInstallation => Get(x => x.DefaultInstallation);
    }

    internal class InstallationSettings
    {
        [SettingsValue(
            Description = "Default installation plugin(s).",
            Tip = "This may be a comma separated value for multiple default installation plugins.",
            NullBehaviour = "equivalent to <code>\"none\"</code> for most unattended usage (unless <code>‑‑source iis</code> is provided) and <code>\"iis\"</code> for interactive mode")]
        public string? DefaultInstallation { get; set; }
    }
}