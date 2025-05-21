namespace PKISharp.WACS.Configuration.Settings
{
    public class PemFilesSettings
    {
        /// <summary>
        /// When using --store pemfiles this path is used by default, saving 
        /// you the effort from providing it manually. Filling this out makes 
        /// the --pemfilespath parameter unnecessary in most cases. Renewals 
        /// created with the default path will automatically change to any 
        /// future default value, meaning this is also a good practice for 
        /// maintainability.
        /// </summary>
        public string? DefaultPath { get; set; }
        /// <summary>
        /// When using --store pemfiles this password is used by default for 
        /// the private key file, saving you the effort from providing it manually. 
        /// Filling this out makes the --pemfilespassword parameter unnecessary in 
        /// most cases. Renewals created with the default password will 
        /// automatically change to any future default value, meaning this
        /// is also a good practice for maintainability.
        /// </summary>
        public string? DefaultPassword { get; set; }
    }
}