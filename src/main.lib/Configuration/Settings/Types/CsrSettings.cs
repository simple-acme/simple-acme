using PKISharp.WACS.Configuration.Settings.Types.Csr;
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Configuration.Settings.Types
{
    public interface ICsrSettings
    {        
        /// <summary> 
        /// Default plugin to select 
        /// </summary>
        string? DefaultCsr { get; }

        /// <summary>
        /// EC key settings
        /// </summary>
        IEcSettings Ec { get; }

        /// <summary>
        /// RSA key settings
        /// </summary>
        IRsaSettings Rsa { get; }
    }

    internal class InheritCsrSettings(params IEnumerable<CsrSettings?> chain) : InheritSettings<CsrSettings>(chain), ICsrSettings
    {
        public string? DefaultCsr => Get(x => x.DefaultCsr);
        public IEcSettings Ec => new InheritEcSettings(Chain.Select(c => c?.Ec));
        public IRsaSettings Rsa => new InheritRsaSettings(Chain.Select(c => c?.Rsa));
    }

    public class CsrSettings
    {
        [SettingsValue(
            Description = "Default CSR plugin.",
            NullBehaviour = "equivalent to <code>\"rsa\"</code>")]
        public string? DefaultCsr { get; set; }

        public RsaSettings? Rsa { get; set; }
        public EcSettings? Ec { get; set; }
    }
}