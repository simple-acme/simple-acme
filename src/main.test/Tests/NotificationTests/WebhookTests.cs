using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Clients;
using PKISharp.WACS.Configuration.Settings;
using PKISharp.WACS.Configuration.Settings.Types;
using PKISharp.WACS.Configuration.Settings.Types.Notification;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.NotificationPlugins;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using PKISharp.WACS.UnitTests.Mock.Services;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace PKISharp.WACS.UnitTests.Tests.NotificationTests
{
    [TestClass]
    public class WebhookTests
    {
        private const string Url = "https://hooks.example.com/services/T000/B000/SECRETPATH";

        private static Renewal Renewal() => new()
        {
            Id = "abc123",
            LastFriendlyName = "example.com",
            PfxPassword = new ProtectedString("pfx-password")
        };

        [TestMethod]
        public void DisabledWithoutUrl()
        {
            using var context = new Fixture();
            context.Webhook.Url = null;
            Assert.IsTrue(context.Target.State.Disabled);
        }

        [TestMethod]
        [DataRow("not a url")]
        [DataRow("ftp://example.com/hook")]
        [DataRow("/relative/hook")]
        public void DisabledWithInvalidUrl(string url)
        {
            using var context = new Fixture();
            context.Webhook.Url = url;
            Assert.IsTrue(context.Target.State.Disabled);
        }

        [TestMethod]
        public void DisabledWithUnknownFormat()
        {
            using var context = new Fixture();
            context.Webhook.Format = "xml";
            Assert.IsTrue(context.Target.State.Disabled);
        }

        [TestMethod]
        public void DisabledWithMissingTemplate()
        {
            using var context = new Fixture();
            context.Webhook.TemplatePath = Path.Combine(context.Directory, "missing.json");
            Assert.IsTrue(context.Target.State.Disabled);
        }

        [TestMethod]
        [DataRow(Url)]
        [DataRow("vault://mock/hook")]
        [DataRow("https://hc-ping.com/uuid/{ExitCode}")]
        public void EnabledWithUrl(string url)
        {
            using var context = new Fixture();
            context.Webhook.Url = url;
            Assert.IsFalse(context.Target.State.Disabled);
        }

        [TestMethod]
        public void NotifyOnSuccessDefaultsToFalse()
        {
            using var context = new Fixture();
            Assert.IsFalse(context.Target.NotifyOnSuccess);
            context.Webhook.NotifyOnSuccess = true;
            Assert.IsTrue(context.Target.NotifyOnSuccess);
        }

        [TestMethod]
        public async Task FailureSendsStandardEvent()
        {
            using var context = new Fixture();

            await context.Target.SendFailure(Renewal(), [], ["Validation failed"]);

            var request = context.Handler.Requests.Single();
            Assert.AreEqual(HttpMethod.Post, request.Method);
            Assert.AreEqual(new Uri(Url), request.Uri);
            Assert.AreEqual("application/json", request.ContentType);
            var json = JsonNode.Parse(request.Body)!;
            Assert.AreEqual(1, json["schemaVersion"]!.GetValue<int>());
            Assert.AreEqual("failure", json["type"]!.GetValue<string>());
            Assert.IsTrue(Guid.TryParse(json["id"]!.GetValue<string>(), out _));
            var data = json["data"]!;
            Assert.AreEqual("error", data["severity"]!.GetValue<string>());
            Assert.AreEqual("down", data["status"]!.GetValue<string>());
            Assert.AreEqual("Renewal for example.com failed", data["title"]!.GetValue<string>());
            Assert.AreEqual("web01", data["computerName"]!.GetValue<string>());
            Assert.AreEqual("abc123", data["renewalId"]!.GetValue<string>());
            Assert.AreEqual("example.com", data["friendlyName"]!.GetValue<string>());
            CollectionAssert.AreEqual(new[] { "Validation failed" }, Strings(data["errors"]));
            Assert.IsNull(data["log"]);
            Assert.IsFalse(request.Body.Contains("pfx-password"));
            Assert.AreEqual(0, context.Errors.Count());
        }

        [TestMethod]
        public async Task SuccessWithErrorsReportsErrorsFromLog()
        {
            using var context = new Fixture();

            await context.Target.SendSuccess(Renewal(), [
                new MemoryEntry(LogEventLevel.Information, "Renewing"),
                new MemoryEntry(LogEventLevel.Error, "Installation failed")]);

            var json = JsonNode.Parse(context.Handler.Requests.Single().Body)!;
            Assert.AreEqual("success-with-errors", json["type"]!.GetValue<string>());
            Assert.AreEqual("warning", json["data"]!["severity"]!.GetValue<string>());
            CollectionAssert.AreEqual(new[] { "Installation failed" }, Strings(json["data"]!["errors"]));
        }

        [TestMethod]
        public async Task EventContainsCertificateInformation()
        {
            using var context = new Fixture();
            var expire = new DateTime(2026, 12, 20, 10, 0, 0, DateTimeKind.Utc);
            var renewal = Renewal();
            renewal.History.Add(new RenewResult
            {
                Success = true,
                OrderResults = [new OrderResult("main") { Success = true, ExpireDate = expire, Thumbprint = "AB12" }]
            });

            await context.Target.SendSuccess(renewal, []);

            var data = JsonNode.Parse(context.Handler.Requests.Single().Body)!["data"]!;
            Assert.AreEqual(expire, data["expiresAt"]!.GetValue<DateTime>().ToUniversalTime());
            Assert.IsNotNull(data["nextDueAt"]);
            CollectionAssert.AreEqual(new[] { "AB12" }, Strings(data["thumbprints"]));
        }

        [TestMethod]
        public async Task ExpiresAtIsEarliestCurrentOrder()
        {
            using var context = new Fixture();
            var renewal = Renewal();
            renewal.History.Add(new RenewResult
            {
                Date = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                Success = true,
                OrderResults = [
                    new OrderResult("first") { Success = true, ExpireDate = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc) },
                    new OrderResult("second") { Success = true, ExpireDate = new DateTime(2026, 12, 5, 0, 0, 0, DateTimeKind.Utc) }]
            });
            // Run that only processed the second order
            renewal.History.Add(new RenewResult
            {
                Date = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
                Success = true,
                OrderResults = [new OrderResult("second") { Success = true, ExpireDate = new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc) }]
            });

            await context.Target.SendSuccess(renewal, []);
            var data = JsonNode.Parse(context.Handler.Requests.Last().Body)!["data"]!;
            Assert.AreEqual(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc), data["expiresAt"]!.GetValue<DateTime>().ToUniversalTime());

            // Partially failed run that renewed the first order
            renewal.History.Add(new RenewResult
            {
                Date = new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc),
                Success = false,
                OrderResults = [
                    new OrderResult("first") { Success = true, ExpireDate = new DateTime(2027, 1, 15, 0, 0, 0, DateTimeKind.Utc) },
                    new OrderResult("second") { Success = false }]
            });

            await context.Target.SendSuccess(renewal, []);
            data = JsonNode.Parse(context.Handler.Requests.Last().Body)!["data"]!;
            Assert.AreEqual(new DateTime(2027, 1, 15, 0, 0, 0, DateTimeKind.Utc), data["expiresAt"]!.GetValue<DateTime>().ToUniversalTime());
        }

        [TestMethod]
        public async Task TokensUseInvariantCulture()
        {
            using var context = new Fixture();
            context.Webhook.TemplatePath = context.WriteFile("template.json", """{ "expires": "{ExpiresAt}" }""");
            var renewal = Renewal();
            renewal.History.Add(new RenewResult
            {
                Success = true,
                OrderResults = [new OrderResult("main") { Success = true, ExpireDate = new DateTime(2026, 12, 20, 10, 0, 0, DateTimeKind.Utc) }]
            });
            var culture = CultureInfo.CurrentCulture;
            try
            {
                // Buddhist calendar and a different time separator
                CultureInfo.CurrentCulture = new CultureInfo("th-TH") { DateTimeFormat = { TimeSeparator = "." } };
                await context.Target.SendSuccess(renewal, []);
            }
            finally
            {
                CultureInfo.CurrentCulture = culture;
            }

            var json = JsonNode.Parse(context.Handler.Requests.Single().Body)!;
            Assert.AreEqual("2026-12-20T10:00:00Z", json["expires"]!.GetValue<string>());
        }

        [TestMethod]
        public async Task IncludeLogLimitsLevelAndLines()
        {
            using var context = new Fixture();
            context.Webhook.IncludeLog = true;
            var log = Enumerable.Range(1, 150).Select(i => new MemoryEntry(LogEventLevel.Information, $"line {i}")).ToList();
            log.Add(new MemoryEntry(LogEventLevel.Debug, "debug line"));

            await context.Target.SendSuccess(Renewal(), log);

            var lines = JsonNode.Parse(context.Handler.Requests.Single().Body)!["data"]!["log"]!.AsArray();
            Assert.AreEqual(100, lines.Count);
            Assert.AreEqual("line 51", lines.First()!["message"]!.GetValue<string>());
            Assert.AreEqual("line 150", lines.Last()!["message"]!.GetValue<string>());
        }

        [TestMethod]
        public async Task TemplateKeepsJsonValid()
        {
            using var context = new Fixture();
            context.Webhook.TemplatePath = context.WriteFile("template.json", """
                {
                    "text": "{FriendlyName}: {Title}",
                    "hosts": "{Hosts}",
                    "errors": "{Errors}",
                    "code": "{ExitCode}",
                    "unknown": "{Unknown}",
                    "nested": [ { "type": "{EventType}" }, 42, true, null ]
                }
                """);
            var renewal = Renewal();
            renewal.LastFriendlyName = "quote\" and\nnewline";

            await context.Target.SendFailure(renewal, [], ["first", "second"]);

            var json = JsonNode.Parse(context.Handler.Requests.Single().Body)!;
            Assert.AreEqual("quote\" and\nnewline: Renewal for quote\" and\nnewline failed", json["text"]!.GetValue<string>());
            Assert.AreEqual(0, json["hosts"]!.AsArray().Count);
            CollectionAssert.AreEqual(new[] { "first", "second" }, Strings(json["errors"]));
            Assert.AreEqual(1, json["code"]!.GetValue<int>());
            Assert.AreEqual("{Unknown}", json["unknown"]!.GetValue<string>());
            Assert.AreEqual("failure", json["nested"]![0]!["type"]!.GetValue<string>());
            Assert.AreEqual(42, json["nested"]![1]!.GetValue<int>());
        }

        [TestMethod]
        public async Task TemplatePathExpandsEnvironmentVariables()
        {
            using var context = new Fixture();
            context.WriteFile("template.json", """{ "type": "{EventType}" }""");
            Environment.SetEnvironmentVariable("WACS_WEBHOOK_TEST", context.Directory);
            try
            {
                context.Webhook.TemplatePath = Path.Combine("%WACS_WEBHOOK_TEST%", "template.json");
                Assert.IsFalse(context.Target.State.Disabled);

                await context.Target.SendFailure(Renewal(), [], ["error"]);
            }
            finally
            {
                Environment.SetEnvironmentVariable("WACS_WEBHOOK_TEST", null);
            }

            var json = JsonNode.Parse(context.Handler.Requests.Single().Body)!;
            Assert.AreEqual("failure", json["type"]!.GetValue<string>());
        }

        [TestMethod]
        public async Task InvalidTemplateIsNotSent()
        {
            using var context = new Fixture();
            context.Webhook.TemplatePath = context.WriteFile("template.json", """{"text": "{Title}" """);

            await context.Target.SendFailure(Renewal(), [], ["error"]);

            Assert.AreEqual(0, context.Handler.Requests.Count);
            Assert.IsTrue(context.Errors.Any(x => x.Contains("not valid JSON")));
        }

        [TestMethod]
        public async Task SlackEscapesAndTruncates()
        {
            using var context = new Fixture();
            context.Webhook.Format = "Slack";
            var renewal = Renewal();
            renewal.LastFriendlyName = "<a&b>";

            await context.Target.SendFailure(renewal, [], [new string('x', 5000)]);

            var json = JsonNode.Parse(context.Handler.Requests.Single().Body)!.AsObject();
            Assert.AreEqual(1, json.Count);
            var text = json["text"]!.GetValue<string>();
            Assert.IsTrue(text.StartsWith("*Renewal for &lt;a&amp;b&gt; failed*\n"));
            Assert.IsTrue(text.Length < 3100);
        }

        [TestMethod]
        public async Task SlackShowsHostsWhenKnown()
        {
            var e = new WebhookEvent
            {
                Data = new WebhookEventData
                {
                    Title = "Certificate livetest renewed",
                    Message = "Certificate livetest successfully renewed on web01.",
                    Hosts = ["example.com", "<www>.example.com"]
                }
            };

            var json = JsonNode.Parse(await WebhookTemplate.Render(WebhookTemplate.SlackTemplate(e), e, x => x.Replace("<", "&lt;").Replace(">", "&gt;")))!;

            Assert.AreEqual("*Certificate livetest renewed*\nCertificate livetest successfully renewed on web01.\nHosts: example.com, &lt;www&gt;.example.com",
                json["text"]!.GetValue<string>());
        }

        [TestMethod]
        public async Task SlackOmitsUnknownHosts()
        {
            using var context = new Fixture();
            context.Webhook.Format = "slack";

            await context.Target.SendTest();

            var text = JsonNode.Parse(context.Handler.Requests.Single().Body)!["text"]!.GetValue<string>();
            Assert.IsFalse(text.Contains("Hosts:"));
        }

        [TestMethod]
        public async Task UrlTokensAreEncoded()
        {
            using var context = new Fixture();
            context.Webhook.Url = "https://hc-ping.com/uuid/{ExitCode}?msg={Title}";

            await context.Target.SendFailure(Renewal(), [], ["error"]);

            Assert.AreEqual("https://hc-ping.com/uuid/1?msg=Renewal%20for%20example.com%20failed",
                context.Handler.Requests.Single().Uri.AbsoluteUri);
        }

        [TestMethod]
        public async Task UrlAndHeadersFromVault()
        {
            using var context = new Fixture();
            await context.Secrets.PutSecret("hook", Url);
            context.Webhook.Url = "vault://mock/hook";
            context.Webhook.Headers = ["X-Auth: vault://mock/key1", "Content-Type: application/cloudevents+json", "invalid"];

            await context.Target.SendTest();

            var request = context.Handler.Requests.Single();
            Assert.AreEqual(new Uri(Url), request.Uri);
            Assert.AreEqual("secret1", request.Headers["X-Auth"]);
            Assert.AreEqual("application/cloudevents+json", request.ContentType);
            Assert.AreEqual("test", JsonNode.Parse(request.Body)!["type"]!.GetValue<string>());
            Assert.IsTrue(context.Lines.Any(x => x.Level == LogEventLevel.Warning && x.Message.Contains("invalid webhook header")));
            Assert.IsTrue(context.Lines.Any(x => x.Message.Contains("Test webhook notification sent")));
        }

        [TestMethod]
        public async Task RetriesTransientErrors()
        {
            using var context = new Fixture();
            context.Handler.Responses.Enqueue(Status(HttpStatusCode.InternalServerError));
            context.Handler.Responses.Enqueue(Status(HttpStatusCode.ServiceUnavailable));

            await context.Target.SendFailure(Renewal(), [], ["error"]);

            Assert.AreEqual(3, context.Handler.Requests.Count);
            CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5) }, context.Delays);
            Assert.AreEqual(0, context.Errors.Count());
        }

        [TestMethod]
        public async Task StopsAfterThreeAttempts()
        {
            using var context = new Fixture();
            for (var i = 0; i < 5; i++)
            {
                context.Handler.Responses.Enqueue(Status(HttpStatusCode.BadGateway));
            }

            await context.Target.SendFailure(Renewal(), [], ["error"]);

            Assert.AreEqual(3, context.Handler.Requests.Count);
            Assert.AreEqual(1, context.Errors.Count());
        }

        [TestMethod]
        public async Task DoesNotRetryClientErrors()
        {
            using var context = new Fixture();
            context.Handler.Responses.Enqueue(Status(HttpStatusCode.NotFound));

            await context.Target.SendFailure(Renewal(), [], ["error"]);

            Assert.AreEqual(1, context.Handler.Requests.Count);
            Assert.IsTrue(context.Errors.Single().Contains("status 404"));
        }

        [TestMethod]
        [DataRow(10, 10)]
        [DataRow(120, 30)]
        [DataRow(1, 2)]
        public async Task FollowsRetryAfter(int retryAfter, int expected)
        {
            using var context = new Fixture();
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(retryAfter));
            context.Handler.Responses.Enqueue(_ => Task.FromResult(response));

            await context.Target.SendFailure(Renewal(), [], ["error"]);

            Assert.AreEqual(2, context.Handler.Requests.Count);
            CollectionAssert.AreEqual(new[] { TimeSpan.FromSeconds(expected) }, context.Delays);
        }

        [TestMethod]
        public async Task RetriesNetworkErrorsAndTimeouts()
        {
            using var context = new Fixture();
            context.Handler.Responses.Enqueue(_ => throw new HttpRequestException("Connection refused (hooks.example.com:443)"));
            context.Handler.Responses.Enqueue(Hang);

            await context.Target.SendFailure(Renewal(), [], ["error"]);

            Assert.AreEqual(3, context.Handler.Requests.Count);
            Assert.AreEqual(0, context.Errors.Count());
            Assert.IsTrue(context.Lines.Any(x => x.Message.Contains("timeout")));
        }

        [TestMethod]
        public async Task RedirectToGetIsFailure()
        {
            using var context = new Fixture();
            context.Handler.Responses.Enqueue(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, Url)
            }));

            await context.Target.SendTest();

            Assert.AreEqual(1, context.Handler.Requests.Count);
            Assert.IsTrue(context.Errors.Single().Contains("redirected"));
            Assert.IsFalse(context.Lines.Any(x => x.Message.Contains("Test webhook notification sent")));
        }

        [TestMethod]
        public async Task LogDoesNotContainSecrets()
        {
            using var context = new Fixture();
            context.Webhook.Headers = ["X-Auth: header-secret"];
            context.Handler.Responses.Enqueue(Status(HttpStatusCode.Forbidden));

            await context.Target.SendFailure(Renewal(), [], ["error"]);

            Assert.IsTrue(context.Errors.Any());
            AssertNoSecrets(context.Lines);
        }

        [TestMethod]
        public async Task NetworkErrorMessageIsNotLogged()
        {
            using var context = new Fixture();
            for (var i = 0; i < 3; i++)
            {
                // The message may contain text from the server
                context.Handler.Responses.Enqueue(_ => throw new HttpRequestException(
                    HttpRequestError.InvalidResponse,
                    "Received an invalid status line: '/services/T000/B000/SECRETPATH'"));
            }

            await context.Target.SendFailure(Renewal(), [], ["error"]);

            AssertNoSecrets(context.Lines);
            Assert.IsTrue(context.Errors.Single().Contains("InvalidResponse"));
        }

        [TestMethod]
        public async Task RequestLoggerRedactsConfidentialRequests()
        {
            var log = new Services.LogService(true, false);
            var logger = new RequestLogger(log);
            using var request = new HttpRequestMessage(HttpMethod.Post, Url) { Content = new StringContent("request-secret") };
            request.Options.Set(RequestLogger.Confidential, true);
            using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                RequestMessage = request,
                Content = new StringContent("response-secret", Encoding.UTF8, "application/json")
            };

            await logger.PreSend(request, CancellationToken.None);
            await logger.PostSend(response, CancellationToken.None);

            Assert.IsTrue(log.Lines.Any(x => x.Message.Contains("https://hooks.example.com")));
            AssertNoSecrets(log.Lines);
            Assert.IsFalse(log.Lines.Any(x => x.Message.Contains("request-secret") || x.Message.Contains("response-secret")));
        }

        [TestMethod]
        public async Task RequestLoggerLogsOtherRequests()
        {
            var log = new Services.LogService(true, false);
            using var request = new HttpRequestMessage(HttpMethod.Post, Url) { Content = new StringContent("request-content") };

            await new RequestLogger(log).PreSend(request, CancellationToken.None);

            Assert.IsTrue(log.Lines.Any(x => x.Message.Contains("SECRETPATH")));
            Assert.IsTrue(log.Lines.Any(x => x.Message.Contains("request-content")));
        }

        [TestMethod]
        public void SettingsFromJson()
        {
            var settings = JsonSerializer.Deserialize("""
                {
                    "Notification": {
                        "Webhook": {
                            "Url": "https://example.com/hook",
                            "Format": "slack",
                            "Headers": [ "X-Auth: value" ],
                            "IncludeLog": true,
                            "NotifyOnSuccess": true
                        }
                    }
                }
                """, SettingsJson.Default.Settings)!;
            var webhook = new InheritSettings(settings).Notification.Webhook!;
            Assert.AreEqual("https://example.com/hook", webhook.Url);
            Assert.AreEqual("slack", webhook.Format);
            CollectionAssert.AreEqual(new[] { "X-Auth: value" }, webhook.Headers.ToList());
            Assert.IsTrue(webhook.IncludeLog);
            Assert.IsTrue(webhook.NotifyOnSuccess);

            var defaults = new InheritSettings(new Settings()).Notification.Webhook!;
            Assert.IsNull(defaults.Url);
            Assert.AreEqual(0, defaults.Headers.Count());
            Assert.IsFalse(defaults.IncludeLog);
            Assert.IsFalse(defaults.NotifyOnSuccess);
        }

        [TestMethod]
        public void SettingsInheritPerValue()
        {
            var global = new Settings
            {
                Notification = new NotificationSettings
                {
                    Webhook = new WebhookNotificationSettings { Url = "https://example.com/hook", Headers = ["X-Auth: value"] }
                }
            };
            var server = new Settings
            {
                Notification = new NotificationSettings
                {
                    Webhook = new WebhookNotificationSettings { NotifyOnSuccess = true }
                }
            };
            var webhook = new InheritSettings(server, global).Notification.Webhook!;
            Assert.AreEqual("https://example.com/hook", webhook.Url);
            CollectionAssert.AreEqual(new[] { "X-Auth: value" }, webhook.Headers.ToList());
            Assert.IsTrue(webhook.NotifyOnSuccess);
        }

        [TestMethod]
        public void SettingsDocumentation()
        {
            var yaml = new StringBuilder();
            YamlService.GenerateTypeYaml(typeof(NotificationSettings), 0, yaml);
            var text = yaml.ToString();
            Assert.IsTrue(text.Contains("Webhook"));
            Assert.IsTrue(text.Contains("TemplatePath"));
        }

        private static void AssertNoSecrets(IEnumerable<MemoryEntry> lines)
        {
            foreach (var line in lines)
            {
                Assert.IsFalse(line.Message.Contains("SECRETPATH"), line.Message);
                Assert.IsFalse(line.Message.Contains("header-secret"), line.Message);
            }
        }

        private static string[] Strings(JsonNode? node) => [.. node!.AsArray().Select(x => x!.GetValue<string>())];

        private static Func<HttpRequestMessage, Task<HttpResponseMessage>> Status(HttpStatusCode status) =>
            _ => Task.FromResult(new HttpResponseMessage(status));

        private static async Task<HttpResponseMessage> Hang(HttpRequestMessage request)
        {
            await Task.Delay(Timeout.Infinite, CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        private sealed class Fixture : IDisposable
        {
            private static readonly Lazy<AssemblyService> Assemblies = new(() => new TestAssemblyService());
            private readonly DirectoryInfo _directory = System.IO.Directory.CreateTempSubdirectory("webhook-test-");
            private readonly IContainer _container;
            private readonly Services.LogService _log = new(true, false);

            public WebhookNotificationSettings Webhook { get; } = new() { Url = Url };
            public RecordingHandler Handler { get; } = new();
            public Mock.Services.SecretService Secrets { get; } = new();
            public List<TimeSpan> Delays { get; } = [];
            public NotificationTargetWebhook Target { get; }
            public string Directory => _directory.FullName;
            public IEnumerable<MemoryEntry> Lines => _log.Lines;
            public IEnumerable<string> Errors => Lines.Where(x => x.Level == LogEventLevel.Error).Select(x => x.Message);

            public Fixture()
            {
                var settings = new MockSettingsService(new Settings
                {
                    Notification = new NotificationSettings { ComputerName = "web01", Webhook = Webhook }
                });
                var builder = new ContainerBuilder();
                builder.RegisterInstance(Secrets);
                _container = builder.Build();
                var input = new Mock.Services.InputService([]);
                var plugins = new PluginService(_log, Assemblies.Value);
                var client = new WebhookClient(_log, new TestProxyService(Handler))
                {
                    Timeout = TimeSpan.FromMilliseconds(100),
                    Delay = x =>
                    {
                        Delays.Add(x);
                        return Task.CompletedTask;
                    }
                };
                Target = new NotificationTargetWebhook(
                    _log,
                    settings,
                    client,
                    new SecretServiceManager(_container, input, plugins, _log),
                    plugins,
                    new DueDateStaticService(new DueDateRuntimeService(settings, _log, input), _log),
                    new Mock.Services.CacheService());
            }

            public string WriteFile(string name, string content)
            {
                var path = Path.Combine(Directory, name);
                File.WriteAllText(path, content);
                return path;
            }

            public void Dispose()
            {
                Handler.Dispose();
                _container.Dispose();
                _directory.Delete(true);
            }
        }

        private sealed class TestAssemblyService : AssemblyService
        {
            public TestAssemblyService() : base(new Mock.Services.LogService())
            {
                _allTypes.Clear();
                _allTypes.AddRange([new(typeof(Mock.Services.SecretService))]);
            }
        }

        private sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? ContentType, string Body, Dictionary<string, string> Headers);

        private sealed class RecordingHandler : HttpMessageHandler
        {
            public List<RecordedRequest> Requests { get; } = [];
            public Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> Responses { get; } = new();

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var body = request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
                Requests.Add(new RecordedRequest(
                    request.Method,
                    request.RequestUri!,
                    request.Content?.Headers.ContentType?.MediaType,
                    body,
                    request.Headers.ToDictionary(x => x.Key, x => string.Join(",", x.Value))));
                var response = Responses.Count > 0 ? Responses.Dequeue() : Status(HttpStatusCode.OK);
                return await response(request).WaitAsync(cancellationToken);
            }
        }

        private sealed class TestProxyService(HttpMessageHandler handler) : IProxyService
        {
            public SslProtocols SslProtocols { get; set; }
            public WindowsProxyUsePolicy ProxyType => throw new NotImplementedException();
            public Task<HttpClient> GetHttpClient(bool checkSsl = true) => Task.FromResult(new HttpClient(handler, false));
            public Task<HttpMessageHandler> GetHttpMessageHandler() => throw new NotImplementedException();
            public Task<IWebProxy?> GetWebProxy() => throw new NotImplementedException();
            public void Disable() => throw new NotImplementedException();
        }
    }
}
