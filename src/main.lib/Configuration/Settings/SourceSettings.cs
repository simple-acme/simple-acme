using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings
{
    public interface ISourceSettings
    {
        /// <summary>
        /// Default plugin to select in the Advanced menu
        /// in the menu.
        string? DefaultSource { get; }
    }

    internal class InheritSourceSettings(IEnumerable<SourceSettings?> chain) : InheritSettings<SourceSettings>(chain), ISourceSettings
    {
        public string? DefaultSource => Get(x => x.DefaultSource);
    }

    internal class SourceSettings : ISourceSettings
    {
        public string? DefaultSource { get; set; }

        /// <summary>
        /// Legacy options converted to their modern equivalents at load time
        /// </summary>
        public string? DefaultTarget { get; set; }
    }
}