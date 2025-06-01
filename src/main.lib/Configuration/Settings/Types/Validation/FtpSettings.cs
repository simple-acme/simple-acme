using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types.Validation
{
    /// <summary>
    /// Settings for FTP validation
    /// </summary>
    public interface IFtpSettings
    {
        /// <summary>
        /// Use GnuTls library for SSL, tradeoff: https://github.com/robinrodricks/FluentFTP/wiki/FTPS-Connection-using-GnuTLS
        /// </summary>
        bool UseGnuTls { get; }
    }

    internal class InheritFtpSettings(params IEnumerable<FtpSettings?> chain) : InheritSettings<FtpSettings>(chain), IFtpSettings
    {
        public bool UseGnuTls => Get(x => x.UseGnuTls) ?? false;
    }

    public class FtpSettings
    {
        [SettingsValue(
            Default = "false",
            Description = "If you experience connection issues with Unix FTPS servers, using the GnuTLS library instead " +
            "of Microsofts native TLS implementation might solve the problem. " +
            "<a href=\"https://github.com/robinrodricks/FluentFTP/wiki/FTPS-Connection-using-GnuTLS\">This page</a> by " +
            "the FluentFTP project explains the reasons behind and limitations of this method. ",
            Warning = "It's not enough to merely change this setting, please refer to the " +
            "documentation of the <a href=\"/reference/plugins/validation/http/ftp\">FTP plugin</a> for more details.")]
        public bool? UseGnuTls { get; set; }
    }
}