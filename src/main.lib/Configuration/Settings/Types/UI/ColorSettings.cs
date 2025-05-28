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
        public string? Background { get; set; }
    }
}