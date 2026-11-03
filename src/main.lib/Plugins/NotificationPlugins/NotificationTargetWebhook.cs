using PKISharp.WACS.Clients;
using PKISharp.WACS.Configuration.Settings.Types.Notification;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Interfaces;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.NotificationPlugins
{
    internal class NotificationTargetWebhook(
        ILogService logService,
        ISettings settings,
        WebhookClient client,
        SecretServiceManager secretServiceManager,
        IPluginService pluginService,
        DueDateStaticService dueDate,
        ICacheService cacheService) : INotificationTarget
    {
        internal const string FormatJson = "json";
        internal const string FormatSlack = "slack";
        private const int MaxLogLines = 100;
        private const int MaxLogLength = 16 * 1024;
        private const int MaxSlackMessage = 3000;

        private IWebhookNotificationSettings? Settings => settings.Notification.Webhook;
        private string Format => string.IsNullOrWhiteSpace(Settings?.Format) ? FormatJson : Settings.Format.Trim().ToLowerInvariant();

        public string Label => "Webhook";
        public bool NotifyOnSuccess => Settings?.NotifyOnSuccess == true;

        /// <summary>
        /// Check if webhook notifications are enabled
        /// </summary>
        public State State
        {
            get
            {
                var url = Settings?.Url;
                if (string.IsNullOrWhiteSpace(url))
                {
                    return State.DisabledState("No URL configured");
                }
                if (!url.StartsWith(SecretServiceManager.VaultPrefix) && ValidUri(url) == null)
                {
                    return State.DisabledState("Invalid URL configured");
                }
                if (!string.IsNullOrWhiteSpace(Settings?.TemplatePath))
                {
                    if (!Settings.TemplatePath.ValidFile(logService))
                    {
                        return State.DisabledState("Invalid template configured");
                    }
                }
                else if (Format != FormatJson && Format != FormatSlack)
                {
                    return State.DisabledState($"Unknown format {Format} configured");
                }
                return State.EnabledState();
            }
        }

        /// <summary>
        /// Handle created notification
        /// </summary>
        public async Task SendCreated(Renewal renewal, IEnumerable<MemoryEntry> log) =>
            await Send(CreateEvent("created", renewal, log, []));

        /// <summary>
        /// Handle success notification
        /// </summary>
        public async Task SendSuccess(Renewal renewal, IEnumerable<MemoryEntry> log)
        {
            var errors = log.Where(l => l.Level == LogEventLevel.Error).Select(l => l.Message).ToList();
            var eventType = errors.Count > 0 ? "success-with-errors" : "success";
            await Send(CreateEvent(eventType, renewal, log, errors));
        }

        /// <summary>
        /// Handle failure notification
        /// </summary>
        public async Task SendFailure(Renewal renewal, IEnumerable<MemoryEntry> log, IEnumerable<string> errors) =>
            await Send(CreateEvent("failure", renewal, log, errors));

        /// <summary>
        /// Handle cancel notification
        /// </summary>
        public async Task SendCancel(Renewal renewal) =>
            await Send(CreateEvent("cancel", renewal, null, []));

        /// <summary>
        /// Handle test notification
        /// </summary>
        public async Task SendTest()
        {
            if (await Send(CreateEvent("test", null, null, [])))
            {
                logService.Information("Test webhook notification sent!");
            }
        }

        /// <summary>
        /// Create the event, without secrets from the renewal
        /// </summary>
        internal WebhookEvent CreateEvent(
            string eventType,
            Renewal? renewal,
            IEnumerable<MemoryEntry>? log,
            IEnumerable<string> errors)
        {
            var computerName = settings.Notification.ComputerName;
            if (string.IsNullOrWhiteSpace(computerName))
            {
                computerName = Environment.MachineName;
            }
            var data = new WebhookEventData
            {
                ComputerName = computerName,
                Client = settings.Client.ClientName,
                ClientVersion = VersionService.SoftwareVersion.ToString(),
                RenewalId = renewal?.Id,
                FriendlyName = renewal?.LastFriendlyName,
                Errors = [.. errors]
            };
            if (renewal != null)
            {
                AddRenewalInfo(data, renewal);
            }
            if (Settings?.IncludeLog == true && log != null)
            {
                data.Log = LogEntries(log);
            }

            var name = renewal?.LastFriendlyName;
            var errorText = string.Join("; ", data.Errors);
            (data.Severity, data.Status, data.Title, data.Message) = eventType switch
            {
                "created" => ("info", "up",
                    $"Certificate {name} created",
                    $"Certificate {name} successfully created on {computerName}."),
                "success" => ("info", "up",
                    $"Certificate {name} renewed",
                    $"Certificate {name} successfully renewed on {computerName}."),
                "success-with-errors" => ("warning", "down",
                    $"Certificate {name} renewed with errors",
                    $"Certificate {name} renewed on {computerName} with errors: {errorText}"),
                "failure" => ("error", "down",
                    $"Renewal for {name} failed",
                    $"Renewal for {name} on {computerName} failed, will retry on next run. Errors: {errorText}"),
                "cancel" => ("info", "up",
                    $"Renewal for {name} cancelled",
                    $"Renewal for {name} on {computerName} has been cancelled."),
                _ => ("info", "up",
                    "Test notification",
                    $"Webhook notifications from {computerName} are working.")
            };

            return new WebhookEvent
            {
                Id = Guid.NewGuid().ToString(),
                Type = eventType,
                Timestamp = DateTime.UtcNow,
                Data = data
            };
        }

        /// <summary>
        /// Information about the certificate(s) and plugins
        /// </summary>
        private void AddRenewalInfo(WebhookEventData data, Renewal renewal)
        {
            var orders = dueDate.CurrentOrders(renewal);
            data.Thumbprints = [.. orders.Select(x => x.LastThumbprint).OfType<string>().Distinct()];
            data.ExpiresAt = ExpiresAt(renewal, orders);
            data.NextDueAt = dueDate.DueDate(renewal)?.Start;
            try
            {
                foreach (var order in orders)
                {
                    var cert = cacheService.PreviousInfo(renewal, order.Key);
                    if (cert != null)
                    {
                        data.Hosts.AddRange(cert.SanNames.Select(x => x.Value));
                    }
                }
                data.Hosts = [.. data.Hosts.Distinct()];
            }
            catch (Exception ex)
            {
                logService.Warning("Unable to read hosts for webhook: {message}", ex.Message);
            }
            try
            {
                data.Plugins = new WebhookPlugins
                {
                    Source = pluginService.GetPlugin(renewal.TargetPluginOptions).Name,
                    Validation = pluginService.GetPlugin(renewal.ValidationPluginOptions).Name,
                    Order = renewal.OrderPluginOptions == null ? null : pluginService.GetPlugin(renewal.OrderPluginOptions).Name,
                    Csr = renewal.CsrPluginOptions == null ? null : pluginService.GetPlugin(renewal.CsrPluginOptions).Name,
                    Store = [.. renewal.StorePluginOptions.Select(x => pluginService.GetPlugin(x).Name)],
                    Installation = [.. renewal.InstallationPluginOptions.Select(x => pluginService.GetPlugin(x).Name)]
                };
            }
            catch (Exception ex)
            {
                logService.Warning("Unable to read plugin information for webhook: {message}", ex.Message);
            }
        }

        /// <summary>
        /// Earliest expiry of the current certificates, based on
        /// the most recent successful result of each order
        /// </summary>
        private static DateTime? ExpiresAt(Renewal renewal, IEnumerable<StaticOrderInfo> orders)
        {
            var results = renewal.History.
                OrderBy(h => h.Date).
                SelectMany(h => h.OrderResults.
                    Where(r => r.Success == true).
                    Select(r => (Key: r.Name.ToLower(), ExpireDate: r.ExpireDate ?? h.ExpireDate))).
                ToList();
            return orders.
                Select(o => results.LastOrDefault(r => r.Key == o.Key).ExpireDate).
                Where(x => x != null).
                Min();
        }

        /// <summary>
        /// Most recent log lines, limited in number and size
        /// </summary>
        private static List<WebhookLogEntry> LogEntries(IEnumerable<MemoryEntry> log)
        {
            var ret = new List<WebhookLogEntry>();
            var length = 0;
            foreach (var entry in log.Where(x => x.Level >= LogEventLevel.Information).Reverse())
            {
                length += entry.Message.Length;
                if (ret.Count == MaxLogLines || length > MaxLogLength)
                {
                    break;
                }
                ret.Add(new WebhookLogEntry { Level = entry.Level.ToString(), Message = entry.Message });
            }
            ret.Reverse();
            return ret;
        }

        /// <summary>
        /// Render and send the event
        /// </summary>
        private async Task<bool> Send(WebhookEvent e)
        {
            var body = await GetBody(e);
            if (body == null)
            {
                return false;
            }
            var uri = await GetUri(e);
            if (uri == null)
            {
                return false;
            }
            return await client.Send(uri, body, await GetHeaders());
        }

        private async Task<string?> GetBody(WebhookEvent e)
        {
            var templatePath = Settings?.TemplatePath;
            try
            {
                if (!string.IsNullOrWhiteSpace(templatePath))
                {
                    var template = await File.ReadAllTextAsync(Environment.ExpandEnvironmentVariables(templatePath));
                    return await WebhookTemplate.Render(template, e);
                }
                if (Format == FormatSlack)
                {
                    if (e.Data.Message.Length > MaxSlackMessage)
                    {
                        e.Data.Message = e.Data.Message[..MaxSlackMessage] + "...";
                    }
                    return await WebhookTemplate.Render(WebhookTemplate.SlackTemplate(e), e, EscapeSlack);
                }
                return JsonSerializer.Serialize(e, WebhookJson.Default.WebhookEvent);
            }
            catch (JsonException ex)
            {
                logService.Error("Webhook template {path} is not valid JSON: {message}", templatePath, ex.Message);
            }
            catch (IOException ex)
            {
                logService.Error("Unable to read webhook template {path}: {message}", templatePath, ex.Message);
            }
            return null;
        }

        /// <summary>
        /// Get the URL from settings or vault and fill tokens
        /// </summary>
        private async Task<Uri?> GetUri(WebhookEvent e)
        {
            var url = Settings?.Url;
            if (url?.StartsWith(SecretServiceManager.VaultPrefix) == true)
            {
                url = await secretServiceManager.EvaluateSecret(url);
            }
            var tokens = WebhookTemplate.Tokens(e).ToDictionary(x => x.Key, x => (string?)Uri.EscapeDataString(x.Value ?? ""));
            var ret = ValidUri(await ScriptClient.ReplaceTokens(url, tokens));
            if (ret == null)
            {
                logService.Error("Invalid webhook URL");
            }
            return ret;
        }

        /// <summary>
        /// Parse headers in the form "Name: value", where the value
        /// may be a vault reference
        /// </summary>
        private async Task<List<KeyValuePair<string, string>>> GetHeaders()
        {
            var ret = new List<KeyValuePair<string, string>>();
            foreach (var header in Settings?.Headers ?? [])
            {
                var split = header.IndexOf(':');
                if (split < 1)
                {
                    logService.Warning("Ignoring invalid webhook header, use the form \"Name: value\"");
                    continue;
                }
                var name = header[..split].Trim();
                var value = header[(split + 1)..].Trim();
                if (value.StartsWith(SecretServiceManager.VaultPrefix))
                {
                    value = await secretServiceManager.EvaluateSecret(value) ?? "";
                }
                ret.Add(new(name, value));
            }
            return ret;
        }

        private static Uri? ValidUri(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) ? uri : null;

        private static string EscapeSlack(string value) =>
            value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
