using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types
{
    public interface IOrderSettings
    {
        /// <summary>
        /// Default plugin to select when none is provided through the 
        /// command line
        /// </summary>
        string? DefaultOrder { get; }

        /// <summary>
        /// Amount of time (in days) that ordered 
        /// certificates should remain valid
        /// </summary>
        int? DefaultValidDays { get; }
    }

    internal class InheritOrderSettings(params IEnumerable<OrderSettings?> chain) : InheritSettings<OrderSettings>(chain), IOrderSettings
    {
        public string? DefaultOrder => Get(x => x.DefaultOrder);
        public int? DefaultValidDays => Get(x => x.DefaultValidDays);
    }

    public class OrderSettings
    {
        [SettingsValue(Description = "Default order plugin.", NullBehaviour = "equivalent to <code>\"single\"</code>")]
        public string? DefaultOrder { get; set; }

        [SettingsValue(
            Default = "null",
            Description = "Number of days requested certificates should remain valid.",
            Warning = "Note that not all servers support this property. Specifically Let's Encrypt " +
            "throws an error when using this at the time of writing.")]
        public int? DefaultValidDays { get; set; } = null;
    }
}