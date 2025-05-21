namespace PKISharp.WACS.Configuration.Settings
{
    public class P7bFileSettings
    {
        /// <summary>
        /// When using --store p7bfile this path is used by default, saving 
        /// you the effort from providing it manually. Filling this out makes 
        /// the --p7bfilepath parameter unnecessary in most cases. Renewals 
        /// created with the default path will automatically change to any 
        /// future default value, meaning this is also a good practice for 
        /// maintainability.
        /// </summary>
        public string? DefaultPath { get; set; }
    }
}