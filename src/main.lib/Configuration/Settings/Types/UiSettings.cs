using PKISharp.WACS.Configuration.Settings.Types.UI;
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Configuration.Settings.Types
{
    public interface IUiSettings
    {
        /// <summary>
        /// Which colors should be applied
        /// </summary>
        IColorSettings Color { get; }

        /// <summary>
        /// A string that is used to format the date of the 
        /// pfx file friendly name. Documentation for 
        /// possibilities is available from Microsoft.
        /// </summary>
        string? DateFormat { get; }

        /// <summary>
        /// The number of hosts to display per page.
        /// </summary>
        int PageSize { get; }

        /// <summary>
        /// How console tekst should be encoded
        /// </summary>
        string? TextEncoding { get; }
    }

    internal class InheritUiSettings(params IEnumerable<UiSettings?> chain) : InheritSettings<UiSettings>(chain), IUiSettings
    {
        public IColorSettings Color => new InheritColorSettings(Chain.Select(c => c?.Color));
        public string? DateFormat => Get(x => x.DateFormat);
        public int PageSize => Get(x => x.PageSize) ?? 50;
        public string? TextEncoding => Get(x => x.TextEncoding);
    }

    internal class UiSettings
    {
        public string? DateFormat { get; set; }
        public int? PageSize { get; set; }
        public string? TextEncoding { get; set; }
        public ColorSettings? Color { get; set; }
    }
}