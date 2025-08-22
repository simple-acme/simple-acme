using System;
using System.Diagnostics.CodeAnalysis;

namespace PKISharp.WACS.Clients.IIS
{
    /// <summary>
    /// This enum defines the flags that can be applied to IIS bindings.
    /// Names are sourced by decompiling the IISAdministration PowerShell module,
    /// found at, because Microsoft is apparently no longer providing updates to the
    /// Microsoft.Web.Administration NuGet package.
    /// https://www.powershellgallery.com/packages/IISAdministration/1.1.0.0
    /// </summary>
    [Flags]
    public enum SSLFlags
    {
        None = 0,
        Sni = 1,
        CentralCertStore = 2,
        DisableHTTP2 = 4,
        DisableOCSPStp = 8,
        DisableQUIC = 16,
        DisableTLS13 = 32,
        DisableLegacyTLS = 64,
        NegotiateClientCert = 128,

        /// <summary>
        /// Flags introduced in specific versions of Windows
        /// </summary>
        IIS10_Flags = IIS10_Server2016_Flags | IIS10_Server2019_Flags | IIS10_Server2025_Flags,
        IIS10_Server2016_Flags = DisableHTTP2 | DisableOCSPStp,
        IIS10_Server2019_Flags = DisableLegacyTLS | DisableTLS13 | DisableQUIC,
        IIS10_Server2025_Flags = NegotiateClientCert,

        /// <summary>
        /// Optional flags (allowed to be provided by the user)
        /// </summary>
        [SuppressMessage("Design", "CA1069:Enums values should not be duplicated", Justification = "Forwards compatibility")]
        OptionalFlags = DisableHTTP2 | DisableOCSPStp | DisableQUIC | DisableTLS13 | DisableLegacyTLS | NegotiateClientCert,

        /// <summary>
        /// Incompatibiliy between certain flags
        /// </summary>
        [SuppressMessage("Design", "CA1069:Enums values should not be duplicated", Justification = "Forwards compatibility")]
        NotWithCentralSsl = DisableHTTP2 | DisableOCSPStp | DisableQUIC | DisableTLS13 | DisableLegacyTLS | NegotiateClientCert
    }
}
