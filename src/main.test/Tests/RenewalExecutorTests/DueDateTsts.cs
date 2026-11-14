using ACMESharp.Protocol.Resources;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Configuration.Settings;
using PKISharp.WACS.Configuration.Settings.Types;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PKISharp.WACS.UnitTests.Tests.RenewalExecutorTests
{
    [TestClass]
    public class DueDateTests
    {
        private readonly DueDateRuntimeService _service = new(new Mock.Services.MockSettingsService(),
            new Mock.Services.LogService(), new Mock.Services.InputService([]));

        [TestMethod]
        [DataRow("""{"suggestedWindow":{"start":"2026-01-02T00:00:00Z","end":"2026-01-02T00:00:00Z"}}""")]
        [DataRow("""{"suggestedWindow":{"start":"2026-01-03T00:00:00Z","end":"2026-01-02T00:00:00Z"}}""")]
        [DataRow("""{"suggestedWindow":{"start":"2026-01-02T00:00:00Z","end":"2026-01-01T19:00:00-05:00"}}""")]
        [DataRow("""{"suggestedWindow":{"start":"2026-01-02T00:00:00Z","end":null}}""")]
        [DataRow("""{"suggestedWindow":{"start":null,"end":"2026-01-02T00:00:00Z"}}""")]
        [DataRow("""{"suggestedWindow":null}""")]
        public void InvalidServerWindowUsesClientSchedule(string response)
        {
            var validFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var validUntil = validFrom.AddDays(90);
            var expected = _service.ComputeDueDate(validFrom, validUntil);
            var actual = _service.ComputeDueDate(validFrom, validUntil, JsonSerializer.Deserialize<AcmeRenewalInfo>(response));

            Assert.AreEqual(expected.Start, actual.Start, "Renewal start");
            Assert.AreEqual(expected.End, actual.End, "Renewal end");
            Assert.AreEqual(expected.Source, actual.Source, "Renewal schedule source");
        }

        [TestMethod]
        [DataRow(10, 11, true)]
        [DataRow(70, 71, false)]
        public void ValidServerWindowKeepsExistingPolicy(int startDay, int endDay, bool earlier)
        {
            var validFrom = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var validUntil = validFrom.AddDays(90);
            var window = new AcmeRenewalInfoSuggestedWindow
            {
                Start = validFrom.AddDays(startDay),
                End = validFrom.AddDays(endDay)
            };
            var expected = earlier
                ? new DomainObjects.DueDate { Start = window.Start.Value, End = window.End.Value, Source = "ri-ri" }
                : _service.ComputeDueDate(validFrom, validUntil);
            var actual = _service.ComputeDueDate(validFrom, validUntil, new AcmeRenewalInfo { SuggestedWindow = window });

            Assert.AreEqual(expected.Start, actual.Start);
            Assert.AreEqual(expected.End, actual.End);
            Assert.AreEqual(expected.Source, actual.Source);
        }

        [DataRow(70, 999, "rd-rp", "2025-03-12")]
        [DataRow(80, 999, "rd-rp", "2025-03-22")]
        [DataRow(85, 999, "rd-rp", "2025-03-27")]
        [DataRow(90, 999, "rd-rp", "2025-04-01")]
        [DataRow(95, 999, "rd-mv", "2025-04-04")]
        [DataRow(99, 999, "rd-mv", "2025-04-04")]
        [TestMethod]
        public void DueDatePercentage(int percentage, int days, string source, string expectedDate)
        {
            var settings = new Settings
            {
                ScheduledTask = new ScheduledTaskSettings() { 
                    RenewalPercentage = percentage,
                    RenewalDays = days
                }
            };
            var inheritSettings = new InheritSettings(settings);
            var log = new Mock.Services.LogService();
            var input = new Mock.Services.InputService(new List<string>([]));
            var dr = new DueDateRuntimeService(inheritSettings, log, input);
            var startDate = DateTime.Parse("2025-01-01");
            var endDate = startDate.AddDays(100);

            var dueDate = dr.ComputeDueDate(validFrom: startDate, validUntil: endDate, renewalInfo: null);
            Assert.AreEqual(source, dueDate.Source);
            Assert.AreEqual(DateTime.Parse(expectedDate), dueDate.End);

        }
    }
}
