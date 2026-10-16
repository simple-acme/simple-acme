using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Configuration.Settings.Types.Validation
{
    public interface ITlsSettings
    {
        ITlsSelfHostingSettings SelfHosting { get; }
    }

    internal class InheritTlsSettings(params IEnumerable<TlsSettings?> chain) : InheritSettings<TlsSettings>(chain), ITlsSettings
    {
        public ITlsSelfHostingSettings SelfHosting => new InheritTlsSelfHostingSettings(Chain.Select(c => c?.SelfHosting));
    }

    public class TlsSettings
    {
        public TlsSelfHostingSettings? SelfHosting { get; set; }
    }
}
