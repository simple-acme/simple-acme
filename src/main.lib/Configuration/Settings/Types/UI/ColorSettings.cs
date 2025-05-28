using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types.UI
{
    /// <summary>
    /// Colors
    /// </summary>
    public interface IColorSettings
    {
        string? Background { get; }
    }

    internal class InheritColorSettings(params IEnumerable<ColorSettings?> chain) : InheritSettings<ColorSettings>(chain), IColorSettings
    {
        public string? Background => Get(x => x.Background);
    }

    internal class ColorSettings
    {
        [SettingsValue(Description = "When set to <code>\"black\"</code>, the background color for the UI will be forced to black using VT100 escape sequences. This only works in modern terminals, i.e. nothing before Windows 2016 / Windows 10.")]
        public string? Background { get; set; }
    }
}