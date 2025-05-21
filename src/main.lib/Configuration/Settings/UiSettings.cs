namespace PKISharp.WACS.Configuration.Settings
{
    public interface IUiSettings
    {
        /// <summary>
        /// Which colors should be applied
        /// </summary>
        IColorSettings? Color { get; }

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

    internal class UiSettings : IUiSettings
    {
        public int PageSize { get; set; } = 50;
        public string? DateFormat { get; set; }
        public string? TextEncoding { get; set; }
        public ColorSettings? Color { get; set; }
        IColorSettings? IUiSettings.Color => Color;
    }
}