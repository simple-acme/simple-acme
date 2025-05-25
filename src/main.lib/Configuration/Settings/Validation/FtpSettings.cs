namespace PKISharp.WACS.Configuration.Settings.Validation
{
    /// <summary>
    /// Settings for FTP validation
    /// </summary>
    public interface IFtpSettings
    {
        /// <summary>
        /// Use GnuTls library for SSL, tradeoff: https://github.com/robinrodricks/FluentFTP/wiki/FTPS-Connection-using-GnuTLS
        /// </summary>
        bool? UseGnuTls { get; }
    }

    internal class FtpSettings : IFtpSettings
    {
        public bool? UseGnuTls { get; set; }
    }
}