using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;

namespace PKISharp.WACS.Plugins.InstallationPlugins
{
    internal class IISArguments : BaseArguments
    {
        private const string SslPortParameterName = "sslport";
        private const string SslIpParameterName = "sslipaddress";
        private const string BindingFlagsParameterName = "iisbindingflags";

        [CommandLine(Description = "Specify site to install new bindings to. Defaults to the source if that is an IIS site.")]
        public long? InstallationSiteId { get; set; }

        [CommandLine(Obsolete = true, Description = "Specify site to install new bindings to. Defaults to the source if that is an IIS site.")]
        public long? FtpSiteId { get; set; }

        [CommandLine(Name = SslPortParameterName, Description = "Port number to use for newly created HTTPS bindings. Defaults to " + IISClient.DefaultBindingPortFormat + ".")]
        public int? SSLPort { get; set; }

        [CommandLine(Name = SslIpParameterName, Description = "IP address to use for newly created HTTPS bindings. Defaults to " + IISClient.DefaultBindingIp + ".")]
        public string? SSLIPAddress { get; set; }

        [CommandLine(Name = BindingFlagsParameterName, Description = $"Flags to apply to IIS bindings. Valid options are " +
            $"`{nameof(SSLFlags.None)}`, " +
            $"`{nameof(SSLFlags.DisableHTTP2)}`, " +
            $"`{nameof(SSLFlags.DisableOCSPStp)}`, " +
            $"`{nameof(SSLFlags.DisableQUIC)}`, " +
            $"`{nameof(SSLFlags.DisableTLS13)}`, " +
            $"`{nameof(SSLFlags.DisableLegacyTLS)}` and " +
            $"`{nameof(SSLFlags.NegotiateClientCert)}`. " +
            "Multiple values may be comma separated. Defaults to settings.json (`Installation.IIS.BindingFlags`).")]
        public string? IISBindingFlags { get; set; }
    }
}
