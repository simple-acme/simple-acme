using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Services;

namespace PKISharp.WACS.UnitTests.Tests.SettingsTests
{
    [TestClass]
    public class AdminServiceTests
    {
        [TestMethod]
        public void IsAdminAndIsSystem_AreMemoized()
        {
            var adminCalls = 0;
            var systemCalls = 0;
            var service = new AdminService(
                determineAdmin: () =>
                {
                    adminCalls++;
                    return true;
                },
                determineSystem: () =>
                {
                    systemCalls++;
                    return false;
                });

            _ = service.IsAdmin;
            _ = service.IsAdmin;
            _ = service.IsSystem;
            _ = service.IsSystem;

            Assert.AreEqual(1, adminCalls);
            Assert.AreEqual(1, systemCalls);
        }
    }
}
