
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Services;
using PKISharp.WACS.UnitTests.Mock.Services;
using System;

namespace PKISharp.WACS.UnitTests.Tests.DomainObjectTests
{
    [TestClass]
    public class DomainParseTest
    {
        [TestMethod]
        public void UpdateTest()
        {
            var log = new Mock.Services.LogService();
            var proxy = new Mock.Services.ProxyService();
            var settings = new MockSettingsService();
            settings.Acme.PublicSuffixListUri = default;
            var x = new DomainParseService(log, proxy, settings);
        }
    }
}
