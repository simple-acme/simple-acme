using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.UnitTests.Mock;
using System;
using System.Linq;
using Real = PKISharp.WACS.Services;

namespace PKISharp.WACS.UnitTests.Tests.RenewalTests
{
    [TestClass]
    public class RenewalManagerTests
    {
        [TestInitialize]
        public void Init()
        {
            if (!OperatingSystem.IsWindows())
            {
                Assert.Inconclusive("Renewal manager integration path is Windows-only in this test harness.");
            }
        }

        [TestMethod]
        public void Simple()
        {
            var container = MockContainer.TestScope(
            [
                "C", // Cancel command
                "y", // Confirm cancel all
                "Q" // Quit
            ]);
            var renewalStore = container.Resolve<Real.IRenewalStore>();
            var renewalManager = container.Resolve<RenewalManager>();
          
            Assert.IsNotNull(renewalManager);
            renewalManager.ManageRenewals().Wait();
            Assert.AreEqual(0, renewalStore.List().Result.Count());
        }

    }
}
