using System;

namespace PKISharp.WACS.Configuration.Settings
{
    public class SourceSettings
    {
        /// <summary>
        /// Default plugin to select in the Advanced menu
        /// in the menu.
        [Obsolete("Use DefaultSource instead")]
        public string? DefaultTarget { get; set; }
        /// <summary>
        /// Default plugin to select in the Advanced menu
        /// in the menu.
        public string? DefaultSource { get; set; }
    }
}