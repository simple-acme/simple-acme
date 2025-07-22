
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Configuration.Settings;
using PKISharp.WACS.Configuration.Settings.Types;
using PKISharp.WACS.Services;
using PKISharp.WACS.UnitTests.Mock.Services;

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
            var settings = new MockSettingsService(
                new Settings() { 
                    Acme = new AcmeSettings() { 
                        PublicSuffixListUri = default 
                    } 
                }
            );
            _ = new DomainParseService(log, proxy, settings);
        }
    }
}
