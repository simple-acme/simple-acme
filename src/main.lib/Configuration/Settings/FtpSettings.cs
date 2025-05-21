namespace PKISharp.WACS.Configuration.Settings
{
    /// <summary>
    /// Settings for FTP validation
    /// </summary>
    public class FtpSettings
    {
        // Use GnuTls library for SSL, tradeoff: https://github.com/robinrodricks/FluentFTP/wiki/FTPS-Connection-using-GnuTLS
        public bool? UseGnuTls { get; set; }
    }
}