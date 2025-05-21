using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings
{
    public class NotificationSettings
    {
        /// <summary>
        /// SMTP server to use for sending email notifications. 
        /// Required to receive renewal failure notifications.
        /// </summary>
        public string? SmtpServer { get; set; }
        /// <summary>
        /// SMTP server port number.
        /// </summary>
        public int SmtpPort { get; set; }
        /// <summary>
        /// User name for the SMTP server, in case 
        /// of authenticated SMTP.
        /// </summary>
        public string? SmtpUser { get; set; }
        /// <summary>
        /// Password for the SMTP server, in case 
        /// of authenticated SMTP.
        /// </summary>
        public string? SmtpPassword { get; set; }
        /// <summary>
        /// Change to True to enable SMTPS.
        /// </summary>
        public bool SmtpSecure { get; set; }
        /// <summary>
        /// 1: Auto (default)
        /// 2: SslOnConnect
        /// 3: StartTls
        /// 4: StartTlsWhenAvailable
        /// </summary>
        public int? SmtpSecureMode { get; set; }
        /// <summary>
        /// Display name to use as the sender of 
        /// notification emails. Defaults to the 
        /// ClientName setting when empty.
        /// </summary>
        public string? SenderName { get; set; }
        /// <summary>
        /// Email address to use as the sender 
        /// of notification emails. Required to 
        /// receive renewal failure notifications.
        /// </summary>
        public string? SenderAddress { get; set; }
        /// <summary>
        /// Email addresses to receive notification emails. 
        /// Required to receive renewal failure 
        /// notifications.
        /// </summary>
        public List<string>? ReceiverAddresses { get; set; }
        /// <summary>
        /// Send an email notification when a certificate 
        /// has been successfully renewed, as opposed to 
        /// the default behavior that only send failure
        /// notifications. Only works if at least 
        /// SmtpServer, SmtpSenderAddressand 
        /// SmtpReceiverAddress have been configured.
        /// </summary>
        public bool EmailOnSuccess { get; set; }
        /// <summary>
        /// Override the computer name that 
        /// is included in the body of the email
        /// </summary>
        public string? ComputerName { get; set; }
    }
}