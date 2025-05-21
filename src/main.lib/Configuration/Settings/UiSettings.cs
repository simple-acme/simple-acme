namespace PKISharp.WACS.Configuration.Settings
{
    public class UiSettings
    {
        /// <summary>
        /// The number of hosts to display per page.
        /// </summary>
        public int PageSize { get; set; } = 50;
        /// <summary>
        /// A string that is used to format the date of the 
        /// pfx file friendly name. Documentation for 
        /// possibilities is available from Microsoft.
        /// </summary>
        public string? DateFormat { get; set; }
        /// <summary>
        /// How console tekst should be encoded
        /// </summary>
        public string? TextEncoding { get; set; }
        /// <summary>
        /// Which colors should be applied
        /// </summary>
        public ColorSettings? Color { get; set; }
    }
}