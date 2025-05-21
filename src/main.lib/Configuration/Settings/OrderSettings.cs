namespace PKISharp.WACS.Configuration.Settings
{
    public interface IOrderSettings
    {
        /// <summary>
        /// Default plugin to select when none is provided through the 
        /// command line
        /// </summary>
        string? DefaultPlugin { get; }

        /// <summary>
        /// Amount of time (in days) that ordered 
        /// certificates should remain valid
        /// </summary>
        int? DefaultValidDays { get; }
    }

    internal class OrderSettings : IOrderSettings
    {
        public string? DefaultPlugin { get; set; }
        public int? DefaultValidDays { get; set; } = null;
    }
}