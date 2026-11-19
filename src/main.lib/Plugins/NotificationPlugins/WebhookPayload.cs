using PKISharp.WACS.Clients;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.NotificationPlugins
{
    /// <summary>
    /// Standard body of a webhook notification. Fields are added
    /// explicitly to make sure that no secrets are included.
    /// </summary>
    internal class WebhookEvent
    {
        public int SchemaVersion { get; set; } = 1;
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public WebhookEventData Data { get; set; } = new();
    }

    internal class WebhookEventData
    {
        public string Severity { get; set; } = "";
        public string Status { get; set; } = "";
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public string? ComputerName { get; set; }
        public string? Client { get; set; }
        public string? ClientVersion { get; set; }
        public string? RenewalId { get; set; }
        public string? FriendlyName { get; set; }
        public List<string> Hosts { get; set; } = [];
        public WebhookPlugins? Plugins { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime? NextDueAt { get; set; }
        public List<string> Thumbprints { get; set; } = [];
        public List<string> Errors { get; set; } = [];
        public List<WebhookLogEntry>? Log { get; set; }
    }

    internal class WebhookPlugins
    {
        public string? Source { get; set; }
        public string? Validation { get; set; }
        public string? Order { get; set; }
        public string? Csr { get; set; }
        public List<string> Store { get; set; } = [];
        public List<string> Installation { get; set; } = [];
    }

    internal class WebhookLogEntry
    {
        public string Level { get; set; } = "";
        public string Message { get; set; } = "";
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(WebhookEvent))]
    internal partial class WebhookJson : JsonSerializerContext { }

    /// <summary>
    /// Fills tokens in a JSON template. Tokens are replaced per string
    /// value, so that the result is always valid JSON.
    /// </summary>
    internal static class WebhookTemplate
    {
        public const string Slack = """{"text":"*{Title}*\n{Message}"}""";
        public const string SlackWithHosts = """{"text":"*{Title}*\n{Message}\nHosts: {Hosts}"}""";

        /// <summary>
        /// Slack template, with the hosts when they are known
        /// (not for test notifications, or after the cache
        /// has been cleared when a renewal is cancelled)
        /// </summary>
        public static string SlackTemplate(WebhookEvent e) => e.Data.Hosts.Count > 0 ? SlackWithHosts : Slack;

        /// <summary>
        /// String value of each token
        /// </summary>
        public static Dictionary<string, string?> Tokens(WebhookEvent e) => new()
        {
            { "EventId", e.Id },
            { "EventType", e.Type },
            { "Timestamp", FormatDate(e.Timestamp) },
            { "Severity", e.Data.Severity },
            { "Status", e.Data.Status },
            { "ExitCode", ExitCode(e).ToString(CultureInfo.InvariantCulture) },
            { "Title", e.Data.Title },
            { "Message", e.Data.Message },
            { "RenewalId", e.Data.RenewalId ?? "" },
            { "FriendlyName", e.Data.FriendlyName ?? "" },
            { "ComputerName", e.Data.ComputerName ?? "" },
            { "Hosts", string.Join(", ", e.Data.Hosts) },
            { "Errors", string.Join("; ", e.Data.Errors) },
            { "ExpiresAt", FormatDate(e.Data.ExpiresAt) },
            { "NextDueAt", FormatDate(e.Data.NextDueAt) }
        };

        public static int ExitCode(WebhookEvent e) => e.Data.Status == "up" ? 0 : 1;

        public static string FormatDate(DateTime? date) => date?.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) ?? "";

        /// <summary>
        /// Render the template for an event
        /// </summary>
        /// <param name="template">JSON template</param>
        /// <param name="e">Event</param>
        /// <param name="escape">Escaping applied to each token value</param>
        /// <exception cref="JsonException">The template is not valid JSON</exception>
        public static async Task<string> Render(string template, WebhookEvent e, Func<string, string>? escape = null)
        {
            var root = JsonNode.Parse(template, documentOptions: new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });
            var tokens = Tokens(e);
            if (escape != null)
            {
                tokens = tokens.ToDictionary(x => x.Key, x => (string?)escape(x.Value ?? ""));
            }
            root = await Replace(root, e, tokens) ?? root;
            return root?.ToJsonString() ?? "null";
        }

        /// <summary>
        /// Replace tokens in a node and its children
        /// </summary>
        /// <returns>New node when the node itself
        /// should be replaced, otherwise null</returns>
        private static async Task<JsonNode?> Replace(JsonNode? node, WebhookEvent e, Dictionary<string, string?> tokens)
        {
            switch (node)
            {
                case JsonObject obj:
                    foreach (var key in obj.Select(x => x.Key).ToList())
                    {
                        var replacement = await Replace(obj[key], e, tokens);
                        if (replacement != null)
                        {
                            obj[key] = replacement;
                        }
                    }
                    return null;
                case JsonArray array:
                    for (var i = 0; i < array.Count; i++)
                    {
                        var replacement = await Replace(array[i], e, tokens);
                        if (replacement != null)
                        {
                            array[i] = replacement;
                        }
                    }
                    return null;
                case JsonValue value when value.GetValueKind() == JsonValueKind.String:
                    var text = value.GetValue<string>();
                    // Keep the type of values that consist of a single token
                    switch (text.Trim().ToLowerInvariant())
                    {
                        case "{hosts}":
                            return new JsonArray([.. e.Data.Hosts.Select(x => JsonValue.Create(x))]);
                        case "{errors}":
                            return new JsonArray([.. e.Data.Errors.Select(x => JsonValue.Create(x))]);
                        case "{exitcode}":
                            return JsonValue.Create(ExitCode(e));
                    }
                    return JsonValue.Create(await ScriptClient.ReplaceTokens(text, tokens));
                default:
                    return null;
            }
        }
    }
}
