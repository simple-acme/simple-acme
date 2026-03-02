using PKISharp.WACS.Clients;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Interfaces;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.NotificationPlugins
{
    internal class NotificationTargetScript : INotificationTarget
    {
        private readonly ILogService _log;
        private readonly ScriptClient _client;
        private readonly string? _script;
        private readonly string _scriptParameters;
        private readonly SecretServiceManager _secretServiceManager;

        public NotificationTargetScript(
            ILogService log,
            ScriptClient client,
            ISettings settings,
            SecretServiceManager secretServiceManager)
        {
            _log = log;
            _client = client;
            _script = settings.Notification.Script?.Script;
            _scriptParameters = settings.Notification.Script?.ScriptParameters ?? "";
            _secretServiceManager = secretServiceManager;
            NotifyOnSuccess = settings.Notification.Script?.NotifyOnSuccess == true;
        }


        public string Label => "Script";

        /// <summary>
        /// Check if script notifications are enabled
        /// </summary>
        public State State
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_script))
                {
                    return State.DisabledState("No script configured");
                }
                if (!_script.ValidFile(_log))
                {
                    return State.DisabledState("Invalid script configured");
                }
                return State.EnabledState();
            }
        }

        public bool NotifyOnSuccess { get; private set; }

        /// <summary>
        /// Handle created notification
        /// </summary>
        public async Task SendCreated(Renewal renewal, IEnumerable<MemoryEntry> log) => 
            await RunScript(renewal, log, "created", null);

        /// <summary>
        /// Handle success notification
        /// </summary>
        public async Task SendSuccess(Renewal renewal, IEnumerable<MemoryEntry> log)
        {
            var withErrors = log.Any(l => l.Level == LogEventLevel.Error);
            var eventType = withErrors ? "success-with-errors" : "success";
            await RunScript(renewal, log, eventType, null);
        }

        /// <summary>
        /// Handle failure notification
        /// </summary>
        public async Task SendFailure(Renewal renewal, IEnumerable<MemoryEntry> log, IEnumerable<string> errors) => 
            await RunScript(renewal, log, "failure", errors);

        /// <summary>
        /// Handle cancel notification
        /// </summary>
        public async Task SendCancel(Renewal renewal) => 
            await RunScript(renewal, null, "cancel", null);

        /// <summary>
        /// Handle test notification
        /// </summary>
        public async Task SendTest()
        {
            var result = await RunScript(null, null, "test", null);
            if (result.Success)
            {
                _log.Information("Test notification script completed!");
            }
            else
            {
                _log.Error("Test notification script failed");
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
                _scriptParameters,
                renewal,
                log,
                eventType,
                errors,
                false);
            var censoredParameters = await ReplaceParameters(
                _scriptParameters,
                renewal,
                log,
                eventType,
                errors,
                true);
            return await _client.RunScript(_script, parameters, censoredParameters);
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
                { "EventType", eventType },
                { "RenewalId", renewal?.Id ?? "" },
                { "FriendlyName", renewal?.LastFriendlyName ?? "" }
            };

            if (errors != null)
            {
                replacements["Errors"] = string.Join("; ", errors);
            }
            else
            {
                replacements["Errors"] = "";
            }

            if (log != null)
            {
                if (censor)
                {
                    replacements["Log"] = "{Log}";
                }
                else
                {
                    var logText = string.Join("\n", log.Select(x => $"{x.Level}: {x.Message}"));
                    replacements["Log"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(logText));
                }
            }
            else
            {
                replacements["Log"] = "";
            }

            return await ScriptClient.ReplaceTokens(input, replacements, _secretServiceManager, censor);
        }
    }
}
