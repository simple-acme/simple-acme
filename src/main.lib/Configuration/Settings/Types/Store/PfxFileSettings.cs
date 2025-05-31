using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types.Store
{
    public interface IPfxFileSettings
    {
        /// <summary>
        /// When using --store pfxfile this password is used by default for 
        /// the pfx files, saving you the effort from providing it manually. 
        /// Filling this out makes the --pfxpassword parameter unnecessary in 
        /// most cases. Renewals created with the default password will 
        /// automatically change to any future default value, meaning this
        /// is also a good practice for maintainability.
        /// </summary>
        string? DefaultPassword { get; }

        /// <summary>
        /// When using --store pfxfile this path is used by default, saving 
        /// you the effort from providing it manually. Filling this out makes 
        /// the --pfxfilepath parameter unnecessary in most cases. Renewals 
        /// created with the default path will automatically change to any 
        /// future default value, meaning this is also a good practice for 
        /// maintainability.
        /// </summary>
        string? DefaultPath { get; }

        /// <summary>
        /// Legacy, SHA256 or Default
        /// </summary>
        string? DefaultProtectionMode { get; }
    }

    internal class InheritPfxFileSettings(params IEnumerable<PfxFileSettings?> chain) : InheritSettings<PfxFileSettings>(chain), IPfxFileSettings
    {
        public string? DefaultPassword => Get(x => x.DefaultPassword);
        public string? DefaultPath => Get(x => x.DefaultPath);
        public string? DefaultProtectionMode => Get(x => x.DefaultProtectionMode);
    }

    internal class PfxFileSettings
    {
        [SettingsValue(
            SubType = "path",
            Description = "When using the <a href=\"/reference/plugins/store/pfxfile\">PFX file</a> plugin this path " +
            "is used by default, saving you the effort of providing it manually. Filling this out makes the " +
            "<code>‑‑pfxfilepath</code> argument unnecessary in most cases. Renewals created with the default path " +
            "will automatically change to any future default value, meaning this is also a good practice for maintainability.")]
        public string? DefaultPath { get; set; }

        [SettingsValue(
            SubType = "secret",
            Description = "When using the <a href=\"/reference/plugins/store/pfxfile\">PFX file</a> plugin this " +
            "password is used by default for the <code>.pfx</code> file, saving you the effort of providing it manually. " +
            "Filling this out makes the <code>‑‑pfxpassword</code> argument unnecessary in most cases. Renewals created" +
            " with the default password will automatically change to any future default value, meaning this is also a" +
            " good practice for maintainability.")]
        public string? DefaultPassword { get; set; }

        [SettingsValue(
            Default = "default",
            SubType = "protectionmode",
            Description = "Determines how the <code>.pfx</code> file will be encrypted.")]
        public string? DefaultProtectionMode { get; set; }
    }
}