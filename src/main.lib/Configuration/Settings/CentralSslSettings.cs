namespace PKISharp.WACS.Configuration.Settings
{
    public class CentralSslSettings
    {
        /// <summary>
        /// When using --store centralssl this path is used by default, saving you
        /// the effort from providing it manually. Filling this out makes the 
        /// --centralsslstore parameter unnecessary in most cases. Renewals 
        /// created with the default path will automatically change to any 
        /// future default value, meaning this is also a good practice for 
        /// maintainability.
        /// </summary>
        public string? DefaultPath { get; set; }
        /// <summary>
        /// When using --store centralssl this password is used by default for 
        /// the pfx files, saving you the effort from providing it manually. 
        /// Filling this out makes the --pfxpassword parameter unnecessary in 
        /// most cases. Renewals created with the default password will 
        /// automatically change to any future default value, meaning this
        /// is also a good practice for maintainability.
        /// </summary>
        public string? DefaultPassword { get; set; }
        /// <summary>
        /// Legacy, SHA256 or Default
        /// </summary>
        public string? DefaultProtectionMode { get; set; }
    }
}