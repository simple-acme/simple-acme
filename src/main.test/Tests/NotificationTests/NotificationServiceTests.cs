using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Interfaces;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.UnitTests.Tests.NotificationTests
{
    [TestClass]
    public class NotificationServiceTests
    {
        private const string TargetError = "Notification target failed";
        private const string ServiceError = "Unable to send notification";

        private readonly Renewal _renewal = new() { LastFriendlyName = "example.com" };

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task CreatedIgnoresFailureOfEarlierTarget(bool throws)
        {
            using var context = new Fixture(throws);

            await context.Service.NotifyCreated(_renewal, context.Log.Lines);

            Assert.AreEqual(nameof(INotificationTarget.SendCreated), context.Recording.Event);
            AssertNoErrors(context.Recording);
            AssertLogged(context.Recording, "created");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task SuccessIgnoresFailureOfEarlierTarget(bool throws)
        {
            using var context = new Fixture(throws);

            await context.Service.NotifySuccess(_renewal, context.Log.Lines);

            Assert.AreEqual(nameof(INotificationTarget.SendSuccess), context.Recording.Event);
            AssertNoErrors(context.Recording);
            AssertLogged(context.Recording, "succeeded");
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task FailureIgnoresFailureOfEarlierTarget(bool throws)
        {
            using var context = new Fixture(throws);

            await context.Service.NotifyFailure(
                RunLevel.Unattended,
                _renewal,
                new RenewResult("Validation failed"),
                context.Log.Lines);

            Assert.AreEqual(nameof(INotificationTarget.SendFailure), context.Recording.Event);
            AssertLogged(context.Recording, "Validation failed");
            Assert.IsFalse(context.Recording.Lines.Any(x => x.Message.Contains(TargetError)));
            Assert.IsFalse(context.Recording.Lines.Any(x => x.Message.Contains(ServiceError)));
        }

        [TestMethod]
        public async Task SuccessStillReportsErrorsFromRenewal()
        {
            using var context = new Fixture(false);
            context.Recording.NotifyOnSuccess = false;
            context.Log.Error("Installation failed");

            await context.Service.NotifySuccess(_renewal, context.Log.Lines);

            // With NotifyOnSuccess off, only the renewal error can trigger this call
            Assert.AreEqual(nameof(INotificationTarget.SendSuccess), context.Recording.Event);
            AssertLogged(context.Recording, "Installation failed");
            Assert.IsFalse(context.Recording.Lines.Any(x => x.Message.Contains(TargetError)));
        }

        private static void AssertNoErrors(RecordingTarget target) =>
            Assert.IsFalse(target.Lines.Any(x => x.Level == LogEventLevel.Error));

        private static void AssertLogged(RecordingTarget target, string message) =>
            Assert.IsTrue(target.Lines.Any(x => x.Message.Contains(message)));

        /// <summary>
        /// Uses the real log service, because the mock
        /// does not record the lines that targets receive
        /// </summary>
        private sealed class Fixture : IDisposable
        {
            private readonly IContainer _container;

            public LogService Log { get; } = new(false, false);
            public RecordingTarget Recording { get; } = new();
            public NotificationService Service { get; }

            public Fixture(bool throws)
            {
                var builder = new ContainerBuilder();
                builder.RegisterInstance(new FailingTarget(Log, throws));
                builder.RegisterInstance(Recording);
                _container = builder.Build();
                var plugins = new PluginService(Log, new TestAssemblyService());
                Service = new NotificationService(_container, Log, plugins);
            }

            public void Dispose() => _container.Dispose();
        }

        private sealed class TestAssemblyService : AssemblyService
        {
            public TestAssemblyService() : base(new Mock.Services.LogService())
            {
                _allTypes.Clear();
                _allTypes.AddRange([new(typeof(FailingTarget)), new(typeof(RecordingTarget))]);
            }
        }

        /// <summary>
        /// Fails like the email target (logs an error) or
        /// like an unhandled exception in any other target
        /// </summary>
        private sealed class FailingTarget(ILogService logService, bool throws) : INotificationTarget
        {
            public string Label => "Failing";
            public State State => State.EnabledState();
            public bool NotifyOnSuccess => true;
            public Task SendCreated(Renewal renewal, IEnumerable<MemoryEntry> log) => Fail();
            public Task SendSuccess(Renewal renewal, IEnumerable<MemoryEntry> log) => Fail();
            public Task SendFailure(Renewal renewal, IEnumerable<MemoryEntry> log, IEnumerable<string> errors) => Fail();
            public Task SendCancel(Renewal renewal) => Fail();
            public Task SendTest() => Fail();

            private Task Fail()
            {
                if (throws)
                {
                    throw new Exception(TargetError);
                }
                logService.Error(TargetError);
                return Task.CompletedTask;
            }
        }

        private sealed class RecordingTarget : INotificationTarget
        {
            public string? Event { get; private set; }
            public List<MemoryEntry> Lines { get; private set; } = [];
            public string Label => "Recording";
            public State State => State.EnabledState();
            public bool NotifyOnSuccess { get; set; } = true;
            public Task SendCreated(Renewal renewal, IEnumerable<MemoryEntry> log) => Record(nameof(SendCreated), log);
            public Task SendSuccess(Renewal renewal, IEnumerable<MemoryEntry> log) => Record(nameof(SendSuccess), log);
            public Task SendFailure(Renewal renewal, IEnumerable<MemoryEntry> log, IEnumerable<string> errors) => Record(nameof(SendFailure), log);
            public Task SendCancel(Renewal renewal) => Record(nameof(SendCancel), []);
            public Task SendTest() => Record(nameof(SendTest), []);

            private Task Record(string name, IEnumerable<MemoryEntry> log)
            {
                Event = name;
                Lines = [.. log];
                return Task.CompletedTask;
            }
        }
    }
}
