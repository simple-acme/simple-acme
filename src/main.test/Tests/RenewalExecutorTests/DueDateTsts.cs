using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Configuration.Settings;
using PKISharp.WACS.Configuration.Settings.Types;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;

namespace PKISharp.WACS.UnitTests.Tests.RenewalExecutorTests
{
    [TestClass]
    public class DueDateTests
    {
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
            Assert.AreEqual(dueDate.End, DateTime.Parse(expectedDate));

        }
    }
}
