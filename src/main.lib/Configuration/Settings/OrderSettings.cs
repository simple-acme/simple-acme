namespace PKISharp.WACS.Configuration.Settings
{
    public class OrderSettings
    {
        /// <summary>
        /// Default plugin to select when none is provided through the 
        /// command line
        /// </summary>
        public string? DefaultPlugin { get; set; }
        /// <summary>
        /// Amount of time (in days) that ordered 
        /// certificates should remain valid
        /// </summary>
        public int? DefaultValidDays { get; set; } = null;
    }
}