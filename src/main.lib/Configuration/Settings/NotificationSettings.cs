using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings
{
    public interface INotificationSettings
    {
        /// <summary>
        /// Override the computer name that 
        /// is included in the body of the email
        /// </summary>
        string? ComputerName { get; }

        /// <summary>
        /// Send an email notification when a certificate 
        /// has been successfully renewed, as opposed to 
        /// the default behavior that only send failure
        /// notifications. Only works if at least 
        /// SmtpServer, SmtpSenderAddressand 
        /// SmtpReceiverAddress have been configured.
        /// </summary>
        bool EmailOnSuccess { get; }

        /// <summary>
        /// Email addresses to receive notification emails. 
        /// Required to receive renewal failure 
        /// notifications.
        /// </summary>
        List<string>? ReceiverAddresses { get; }

        /// <summary>
        /// Email address to use as the sender 
        /// of notification emails. Required to 
        /// receive renewal failure notifications.
        /// </summary>
        string? SenderAddress { get; }

        /// <summary>
        /// Display name to use as the sender of 
        /// notification emails. Defaults to the 
        /// ClientName setting when empty.
        /// </summary>
        string? SenderName { get; }

        /// <summary>
        /// Password for the SMTP server, in case 
        /// of authenticated SMTP.
        /// </summary>
        string? SmtpPassword { get; }

        /// <summary>
        /// SMTP server port number.
        /// </summary>
        int SmtpPort { get; }

        /// <summary>
        /// Change to True to enable SMTPS.
        /// </summary>
        bool SmtpSecure { get; }

        /// <summary>
        /// 1: Auto (default)
        /// 2: SslOnConnect
        /// 3: StartTls
        /// 4: StartTlsWhenAvailable
        /// </summary>
        int? SmtpSecureMode { get; }

        /// <summary>
        /// SMTP server to use for sending email notifications. 
        /// Required to receive renewal failure notifications.
        /// </summary>
        string? SmtpServer { get; }

        /// <summary>
        /// User name for the SMTP server, in case 
        /// of authenticated SMTP.
        /// </summary>
        string? SmtpUser { get; }
    }

    internal class NotificationSettings : INotificationSettings
    {
        public string? SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public string? SmtpUser { get; set; }
        public string? SmtpPassword { get; set; }
        public bool SmtpSecure { get; set; }
        public int? SmtpSecureMode { get; set; }
        public string? SenderName { get; set; }
        public string? SenderAddress { get; set; }
        public List<string>? ReceiverAddresses { get; set; }
        public bool EmailOnSuccess { get; set; }
        public string? ComputerName { get; set; }
    }
}