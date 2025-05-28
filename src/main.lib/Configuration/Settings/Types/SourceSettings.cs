using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types
{
    public interface ISourceSettings
    {
        /// <summary>
        /// Default plugin to select in the Advanced menu
        /// in the menu.
        string? DefaultSource { get; }
    }

    internal class InheritSourceSettings(params IEnumerable<SourceSettings?> chain) : InheritSettings<SourceSettings>(chain), ISourceSettings
    {
        public string? DefaultSource => Get(x => x.DefaultSource);
    }

    internal class SourceSettings
    {
        [SettingsValue(
            Description = "Default source plugin. This only affects the menu in the UI.",
            NullBehaviour = "equivalent to <code>\"iis\"</code>, with <code>\"manual\"</code> as backup and unprivileged users or systems without IIS")]
        public string? DefaultSource { get; set; }

        [SettingsValue(Hidden = true)]
        public string? DefaultTarget { get; set; }
    }
}