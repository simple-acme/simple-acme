using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types.Store
{
    public interface IPemFilesSettings
    {
        /// <summary>
        /// When using --store pemfiles this password is used by default for 
        /// the private key file, saving you the effort from providing it manually. 
        /// Filling this out makes the --pemfilespassword parameter unnecessary in 
        /// most cases. Renewals created with the default password will 
        /// automatically change to any future default value, meaning this
        /// is also a good practice for maintainability.
        /// </summary>
        string? DefaultPassword { get; }

        /// <summary>
        /// When using --store pemfiles this path is used by default, saving 
        /// you the effort from providing it manually. Filling this out makes 
        /// the --pemfilespath parameter unnecessary in most cases. Renewals 
        /// created with the default path will automatically change to any 
        /// future default value, meaning this is also a good practice for 
        /// maintainability.
        /// </summary>
        string? DefaultPath { get; }
    }

    internal class InheritPemFilesSettings(params IEnumerable<PemFilesSettings?> chain) : InheritSettings<PemFilesSettings>(chain), IPemFilesSettings
    {
        public string? DefaultPassword => Get(x => x.DefaultPassword);
        public string? DefaultPath => Get(x => x.DefaultPath);
    }

    internal class PemFilesSettings
    {
        public string? DefaultPath { get; set; }
        public string? DefaultPassword { get; set; }
    }
}