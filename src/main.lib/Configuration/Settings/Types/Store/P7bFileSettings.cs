using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types.Store
{
    public interface IP7bFileSettings
    {
        /// <summary>
        /// When using --store p7bfile this path is used by default, saving 
        /// you the effort from providing it manually. Filling this out makes 
        /// the --p7bfilepath parameter unnecessary in most cases. Renewals 
        /// created with the default path will automatically change to any 
        /// future default value, meaning this is also a good practice for 
        /// maintainability.
        /// </summary>
        string? DefaultPath { get; }
    }

    internal class InheritP7bFileSettings(params IEnumerable<P7bFileSettings?> chain) : InheritSettings<P7bFileSettings>(chain), IP7bFileSettings
    {
        public string? DefaultPath => Get(x => x.DefaultPath);
    }

    internal class P7bFileSettings
    {
        public string? DefaultPath { get; set; }
    }
}