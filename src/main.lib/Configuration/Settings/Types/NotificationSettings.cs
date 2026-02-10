using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types
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
        IEnumerable<string> ReceiverAddresses { get; }

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
        int SmtpSecureMode { get; }

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

        /// <summary>
        /// Path to external script or program to run for notifications.
        /// Supported extensions: .ps1, .sh, .exe, .bat, .cmd (Windows only).
        /// </summary>
        string? Script { get; }

        /// <summary>
        /// Parameters for the notification script. May include tokens 
        /// like {EventType}, {RenewalId}, {FriendlyName}, {Errors}, 
        /// {Log} (base64-encoded), and {vault://json/key} for secrets.
        /// </summary>
        string? ScriptParameters { get; }

        /// <summary>
        /// Send a script notification when a certificate 
        /// has been successfully created or renewed, as 
        /// opposed to the default behavior that only sends
        /// failure notifications.
        /// </summary>
        bool ScriptNotifyOnSuccess { get; }
    }

    internal class InheritNotificationSettings(params IEnumerable<NotificationSettings?> chain) : InheritSettings<NotificationSettings>(chain), INotificationSettings
    {
        public string? ComputerName => Get(x => x.ComputerName);
        public bool EmailOnSuccess => Get(x => x.EmailOnSuccess) ?? true;
        public IEnumerable<string> ReceiverAddresses => Get(x => x.ReceiverAddresses) ?? [];
        public string? SenderAddress => Get(x => x.SenderAddress);
        public string? SenderName => Get(x => x.SenderName);
        public string? SmtpPassword => Get(x => x.SmtpPassword);
        public int SmtpPort => Get(x => x.SmtpPort) ?? 25;
        public bool SmtpSecure => Get(x => x.SmtpSecure) ?? true;
        public int SmtpSecureMode => Get(x => x.SmtpSecureMode) ?? 1;
        public string? SmtpServer => Get(x => x.SmtpServer);
        public string? SmtpUser => Get(x => x.SmtpUser);
        public string? Script => Get(x => x.Script);
        public string? ScriptParameters => Get(x => x.ScriptParameters);
        public bool ScriptNotifyOnSuccess => Get(x => x.ScriptNotifyOnSuccess) ?? true;
    }

    public class NotificationSettings
    {
        [SettingsValue(
            SubType = "host", 
            Description = "SMTP server to use for sending email notifications. Required to receive renewal failure notifications.")]
        public string? SmtpServer { get; set; }

        [SettingsValue(
            Default = "25",
            Description = "SMTP server port number.")]
        public int? SmtpPort { get; set; }

        [SettingsValue(
            Description = "User name for the SMTP server, in case of authenticated SMTP.")]
        public string? SmtpUser { get; set; }

        [SettingsValue(
            SubType = "secret",
            Description = "Password for the SMTP server, in case of authenticated SMTP.")]
        public string? SmtpPassword { get; set; }

        [SettingsValue(
            Default = "false",
            Description = "Change to <code>true</code> to enable secure SMTP.")]
        public bool? SmtpSecure { get; set; }

        [SettingsValue(
            Default = "1",
            Description = "Control the way the connection with the mail server is established. " +
            "Only change this if you run into connection issues." +
            "<div class=\"callout-block callout-block-success mt-3\">" +
            "<div class=\"content\">" +
            "<table class=\"table table-bordered\">" +
            "<tr><th class=\"col-md-3\">Value</th><th>Meaning</th></tr>" +
            "<tr><td>1</td><td>Automatic (based on port number)</td></tr>" +
            "<tr><td>2</td><td>Implicit TLS</td></tr>" +
            "<tr><td>3</td><td>Explicit TLS (required)</td></tr>" +
            "<tr><td>4</td><td>Explicit TLS (when available)</td></tr>" +
            "</table></div></div>")]
        public int? SmtpSecureMode { get; set; }

        [SettingsValue(
            Description = "Display name to use as the sender of notification emails.", 
            NullBehaviour = "equivalent to <code>{Client.ClientName}</code>")]
        public string? SenderName { get; set; }

        [SettingsValue(
            Description = "Email address to use as the sender of notification emails. Required to receive renewal notifications.",
            SubType = "email")]
        public string? SenderAddress { get; set; }

        [SettingsValue(
            Description = "Email address to use as the sender of notification emails. " +
            "Required to receive renewal failure notifications. The correct format for the receiver is " +
            "<code>[\"example@example.com\"]</code> for a single address and " +
            "<code>[\"example1@example.com\", \"example2@example.com\"]</code> for multiple addresses.",
            SubType = "email")]
        public List<string>? ReceiverAddresses { get; set; }

        [SettingsValue(
            Default = "false",
            Description = "Send an email notification when a certificate has been successfully created or " +
            "renewed, as opposed to the default behavior that only send failure notifications. Only works " +
            "if at least <code>SmtpServer</code>, <code>SmtpSenderAddress</code> and<code>SmtpReceiverAddress</code> " +
            "have been configured.")]
        public bool? EmailOnSuccess { get; set; }

        [SettingsValue(Description = "This value replaces the computer machine name reported in emails.")]
        public string? ComputerName { get; set; }

        [SettingsValue(
            Description = "Path to external script or program to run for notifications. " +
            "Supported extensions: <code>.ps1</code>, <code>.sh</code>, <code>.exe</code>, " +
            "<code>.bat</code>, <code>.cmd</code> (Windows only).")]
        public string? Script { get; set; }

        [SettingsValue(
            Description = "Parameters for the notification script. May include tokens like " +
            "<code>{EventType}</code> (created, success, success-with-errors, failure, cancel, test), " +
            "<code>{RenewalId}</code>, <code>{FriendlyName}</code>, <code>{Errors}</code>, " +
            "<code>{Log}</code> (base64-encoded), and <code>{vault://json/key}</code> for secrets.")]
        public string? ScriptParameters { get; set; }

        [SettingsValue(
            Default = "false",
            Description = "Send a script notification when a certificate has been successfully created or " +
            "renewed, as opposed to the default behavior that only sends failure notifications.")]
        public bool? ScriptNotifyOnSuccess { get; set; }
    }
}