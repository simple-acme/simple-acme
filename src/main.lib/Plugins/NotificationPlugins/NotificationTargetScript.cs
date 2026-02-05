using PKISharp.WACS.Clients;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Interfaces;
using Serilog.Events;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.NotificationPlugins
{
    internal class NotificationTargetScript : INotificationTarget
    {
        private readonly ILogService _log;
        private readonly ScriptClient _client;
        private readonly ISettings _settings;
        private readonly SecretServiceManager _secretServiceManager;

        public NotificationTargetScript(
            ILogService log,
            ScriptClient client,
            ISettings settings,
            SecretServiceManager secretServiceManager)
        {
            _log = log;
            _client = client;
            _settings = settings;
            _secretServiceManager = secretServiceManager;
            _log.Verbose("NotificationTargetScript initialized. Script configured: {enabled}", Enabled);
        }

        /// <summary>
        /// Check if script notifications are enabled
        /// </summary>
        private bool Enabled => !string.IsNullOrWhiteSpace(_settings.Notification.Script);

        /// <summary>
        /// Handle created notification
        /// </summary>
        public async Task SendCreated(Renewal renewal, IEnumerable<MemoryEntry> log)
        {
            if (Enabled)
            {
                await RunScript(renewal, log, "created", null);
            }
        }

        /// <summary>
        /// Handle success notification
        /// </summary>
        public async Task SendSuccess(Renewal renewal, IEnumerable<MemoryEntry> log)
        {
            if (Enabled)
            {
                var withErrors = log.Any(l => l.Level == LogEventLevel.Error);
                var eventType = withErrors ? "success-with-errors" : "success";
                await RunScript(renewal, log, eventType, null);
            }
        }

        /// <summary>
        /// Handle failure notification
        /// </summary>
        public async Task SendFailure(Renewal renewal, IEnumerable<MemoryEntry> log, IEnumerable<string> errors)
        {
            if (Enabled)
            {
                await RunScript(renewal, log, "failure", errors);
            }
        }

        /// <summary>
        /// Handle cancel notification
        /// </summary>
        public async Task SendCancel(Renewal renewal)
        {
            if (Enabled)
            {
                await RunScript(renewal, null, "cancel", null);
            }
        }

        /// <summary>
        /// Handle test notification
        /// </summary>
        public async Task SendTest()
        {
            _log.Verbose("NotificationTargetScript.SendTest() called. Enabled: {enabled}", Enabled);
            if (!Enabled)
            {
                _log.Information("Script notifications not configured.");
            }
            else
            {
                _log.Information("Sending test notification...");
                var result = await RunScript(null, null, "test", null);
                if (result.Success)
                {
                    _log.Information("Test notification script completed successfully!");
                }
                else
                {
                    _log.Error("Test notification script failed.");
                }
            }
        }

        /// <summary>
        /// Run the notification script
        /// </summary>
        private async Task<ScriptResult> RunScript(
            Renewal? renewal,
            IEnumerable<MemoryEntry>? log,
            string eventType,
            IEnumerable<string>? errors)
        {
            var parameters = await ReplaceParameters(
                _settings.Notification.ScriptParameters ?? "",
                renewal,
                log,
                eventType,
                errors,
                false);
            var censoredParameters = await ReplaceParameters(
                _settings.Notification.ScriptParameters ?? "",
                renewal,
                log,
                eventType,
                errors,
                true);
            return await _client.RunScript(_settings.Notification.Script!, parameters, censoredParameters);
        }

        /// <summary>
        /// Replace parameters with actual values
        /// </summary>
        private async Task<string> ReplaceParameters(
            string input,
            Renewal? renewal,
            IEnumerable<MemoryEntry>? log,
            string eventType,
            IEnumerable<string>? errors,
            bool censor)
        {
            var replacements = new Dictionary<string, string?>
            {
                { "EventType", $"\"{eventType}\"" },
                { "RenewalId", $"\"{renewal?.Id ?? ""}\"" },
                { "FriendlyName", $"\"{renewal?.LastFriendlyName ?? ""}\"" }
            };

            if (errors != null)
            {
                replacements["Errors"] = $"\"{string.Join("; ", errors)}\"";
            }
            else
            {
                replacements["Errors"] = "\"\"";
            }

            if (log != null)
            {
                var logText = string.Join("\n", log.Select(x => $"{x.Level}: {x.Message}"));
                replacements["Log"] = $"\"{System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(logText))}\"";
            }
            else
            {
                replacements["Log"] = "\"\"";
            }

            return await ScriptClient.ReplaceTokens(input, replacements, _secretServiceManager, censor);
        }
    }
}
