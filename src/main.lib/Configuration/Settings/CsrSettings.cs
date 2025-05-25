using PKISharp.WACS.Configuration.Settings.Csr;
using PKISharp.WACS.Configuration.Settings.UI;
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Configuration.Settings
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

    internal class CsrSettings
    {
        public string? DefaultCsr { get; set; }
        public RsaSettings? Rsa { get; set; }
        public EcSettings? Ec { get; set; }
    }
}