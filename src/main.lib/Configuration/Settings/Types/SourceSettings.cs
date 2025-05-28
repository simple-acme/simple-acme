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
        public string? DefaultSource { get; set; }

        /// <summary>
        /// Legacy options converted to their modern equivalents at load time
        /// </summary>
        public string? DefaultTarget { get; set; }
    }
}