using PKISharp.WACS.Configuration.Settings.Types.Notification;
using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types
{
    public interface INotificationSettings
    {
        /// <summary>
        /// Override the computer name that 
        /// is included in the notification
        /// </summary>
        string? ComputerName { get; }

        IEmailSettings? Email { get; }

        IScriptNotificationSettings? Script { get; }
    }

    internal class InheritNotificationSettings(params IEnumerable<NotificationSettings?> chain) : InheritSettings<NotificationSettings>(chain), INotificationSettings
    {
        public string? ComputerName => Get(x => x.ComputerName);
        public IEmailSettings? Email => new InheritEmailSettings(Get(x => x.Email));
        public IScriptNotificationSettings? Script => new InheritScriptNotificationSettings(Get(x => x.Script));
    }

    public class NotificationSettings
    {
        [SettingsValue(Description = "This value replaces the computer machine name reported in notifications.")]
        public string? ComputerName { get; set; }

        [SettingsValue(Split = true)]
        public EmailSettings? Email { get; set; }

        [SettingsValue(Split = true)]
        public ScriptNotificationSettings? Script { get; set; }

        // BACKWARDS COMPATIBILITY: these settings were previously in the root,
        // so we keep them here for backwards compatibility, but hide them from the
        // docs since they are now available under the <code>Email</code> section.

        [SettingsValue(Hidden = true)]
        public string? SmtpServer { get; set; }

        [SettingsValue(Hidden = true)]
        public int? SmtpPort { get; set; }

        [SettingsValue(Hidden = true)]
        public string? SmtpUser { get; set; }

        [SettingsValue(Hidden = true)]
        public string? SmtpPassword { get; set; }

        [SettingsValue(Hidden = true)]
        public bool? SmtpSecure { get; set; }

        [SettingsValue(Hidden = true)]
        public int? SmtpSecureMode { get; set; }

        [SettingsValue(Hidden = true)]
        public string? SenderName { get; set; }

        [SettingsValue(Hidden = true)]
        public string? SenderAddress { get; set; }

        [SettingsValue(Hidden = true)]
        public List<string>? ReceiverAddresses { get; set; }

        [SettingsValue(Hidden = true)]
        public bool? EmailOnSuccess { get; set; }
    }
}