using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.Extensions;
using System;
using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types.Installation
{
    public interface IIISSettings
    {
        SSLFlags BindingFlags { get; }
    }

    internal class InheritIISSettings(params IEnumerable<IISSettings?> chain) : InheritSettings<IISSettings>(chain), IIISSettings
    {
        public SSLFlags BindingFlags
        {
            get
            {
                var ret = SSLFlags.None;
                var raw = Get(x => x.BindingFlags).ParseCsv();
                if (raw == null || raw.Count == 0)
                {
                    return ret;
                }
                foreach (var part in raw)
                {
                    if (Enum.TryParse<SSLFlags>(part, true, out var flag))
                    {
                        ret |= flag;
                    }
                }
                return ret;
            }
        }
    }

    public class IISSettings
    {
        [SettingsValue(
            Description = $"Flags to apply to newly created IIS bindings. Valid options are " +
            $"<code>{nameof(SSLFlags.DisableHTTP2)}</code>, " +
            $"<code>{nameof(SSLFlags.DisableOCSPStp)}</code>, " +
            $"<code>{nameof(SSLFlags.DisableQUIC)}</code>, " +
            $"<code>{nameof(SSLFlags.DisableTLS13)}</code>, " +
            $"<code>{nameof(SSLFlags.DisableLegacyTLS)}</code> and " + 
            $"<code>{nameof(SSLFlags.NegotiateClientCert)}</code>. " +
            "Multiple values may be comma separated.")]
        public string? BindingFlags { get; set; }
    }
}