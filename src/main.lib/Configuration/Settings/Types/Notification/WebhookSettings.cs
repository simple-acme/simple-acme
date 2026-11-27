using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types.Notification
{
    public interface IWebhookNotificationSettings
    {
        /// <summary>
        /// URL that receives the notifications as a POST request,
        /// or a vault:// reference to a secret that contains it.
        /// </summary>
        string? Url { get; }

        /// <summary>
        /// Body format, either "json" (default) or "slack"
        /// </summary>
        string? Format { get; }

        /// <summary>
        /// Path to a JSON template that replaces the default body
        /// </summary>
        string? TemplatePath { get; }

        /// <summary>
        /// Extra request headers in the form "Name: value"
        /// </summary>
        IEnumerable<string> Headers { get; }

        /// <summary>
        /// Include the log of the renewal in the request
        /// </summary>
        bool IncludeLog { get; }

        /// <summary>
        /// Send a notification when a certificate has
        /// been successfully created or renewed
        /// </summary>
        bool NotifyOnSuccess { get; }
    }

    internal class InheritWebhookNotificationSettings(params IEnumerable<WebhookNotificationSettings?> chain) : InheritSettings<WebhookNotificationSettings>(chain), IWebhookNotificationSettings
    {
        public string? Url => Get(x => x.Url);
        public string? Format => Get(x => x.Format);
        public string? TemplatePath => Get(x => x.TemplatePath);
        public IEnumerable<string> Headers => Get(x => x.Headers) ?? [];
        public bool IncludeLog => Get(x => x.IncludeLog) ?? false;
        public bool NotifyOnSuccess => Get(x => x.NotifyOnSuccess) ?? false;
    }

    public class WebhookNotificationSettings
    {
        [SettingsValue(
            Description = "URL that receives the notifications as a <code>POST</code> request. " +
            "Webhook URLs often contain a secret, so you may store it in the vault and use a full " +
            "reference like <code>vault://json/mykey</code> here. The URL may contain the same tokens " +
            "as the template, which are URL-encoded, e.g. <code>{ExitCode}</code> for health check services.",
            NullBehaviour = "disables webhook notifications")]
        public string? Url { get; set; }

        [SettingsValue(
            Default = "json",
            Description = "Format of the request body. Use <code>json</code> for the standard " +
            "event or <code>slack</code> for Slack and Slack-compatible services like Discord " +
            "(<code>&lt;webhook&gt;/slack</code>) and Mattermost.")]
        public string? Format { get; set; }

        [SettingsValue(
            Description = "Path to a JSON file that is used as the request body instead of the " +
            "<code>Format</code>. Every string value in the file may contain tokens like " +
            "<code>{EventType}</code>, <code>{Severity}</code>, <code>{Status}</code>, " +
            "<code>{ExitCode}</code>, <code>{Title}</code>, <code>{Message}</code>, " +
            "<code>{RenewalId}</code>, <code>{FriendlyName}</code>, <code>{ComputerName}</code>, " +
            "<code>{Hosts}</code>, <code>{Errors}</code>, <code>{ExpiresAt}</code>, " +
            "<code>{NextDueAt}</code>, <code>{EventId}</code> and <code>{Timestamp}</code>. " +
            "A value that consists of only <code>{Hosts}</code> or <code>{Errors}</code> " +
            "becomes an array, and only <code>{ExitCode}</code> becomes a number.")]
        public string? TemplatePath { get; set; }

        [SettingsValue(
            Description = "Extra request headers in the form <code>Name: value</code>, for example " +
            "for authentication. The value may be a full vault reference like " +
            "<code>vault://json/mykey</code>.")]
        public List<string>? Headers { get; set; }

        [SettingsValue(
            Default = "false",
            Description = "Include log lines of the renewal (Information level and up, " +
            "at most 100 lines) in the standard JSON event.")]
        public bool? IncludeLog { get; set; }

        [SettingsValue(
            Default = "false",
            Description = "Send a webhook notification when a certificate has been successfully created or " +
            "renewed, as opposed to the default behavior that only sends failure notifications.")]
        public bool? NotifyOnSuccess { get; set; }
    }
}
