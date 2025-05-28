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

    internal class FtpSettings
    {
        public bool? UseGnuTls { get; set; }
    }
}