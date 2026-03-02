using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types.Notification
{
    public interface IScriptNotificationSettings
    {
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
        bool NotifyOnSuccess { get; }
    }

    internal class InheritScriptNotificationSettings(params IEnumerable<ScriptNotificationSettings?> chain) : InheritSettings<ScriptNotificationSettings>(chain), IScriptNotificationSettings
    {
        public string? Script => Get(x => x.Script);
        public string? ScriptParameters => Get(x => x.ScriptParameters);
        public bool NotifyOnSuccess => Get(x => x.NotifyOnSuccess) ?? true;
    }

    public class ScriptNotificationSettings
    {
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
        public bool? NotifyOnSuccess { get; set; }
    }
}
