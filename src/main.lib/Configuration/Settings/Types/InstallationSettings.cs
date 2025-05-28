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
        public string? DefaultInstallation { get; set; }
    }
}