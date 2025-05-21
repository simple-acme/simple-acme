using System;

namespace PKISharp.WACS.Configuration.Settings
{
    public interface ISourceSettings
    {
        /// <summary>
        /// Default plugin to select in the Advanced menu
        /// in the menu.
        string? DefaultSource { get; }
 
        [Obsolete("Use DefaultSource instead")]
        string? DefaultTarget { get; }
    }

    internal class SourceSettings : ISourceSettings
    {
        public string? DefaultTarget { get; set; }
        public string? DefaultSource { get; set; }
    }
}