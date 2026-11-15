using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Services.Encryption;
using PKISharp.WACS.Services.Serialization;
using System.Runtime.Versioning;
using System;

namespace PKISharp.WACS.UnitTests.Tests.EcnryptionTests
{
    [TestClass]
    public class EncryptionTests
    {
        [SupportedOSPlatform("windows")]
        [TestMethod]
        [OSCondition(OperatingSystems.Windows)]
        public void TurnOnOff()
        {
            // Create encrypted value
            var plain = "---BLA---";
            var psFactory = new ProtectedStringFactory(new DpApiProtector(), new Mock.Services.LogService(false));
            var plainString = psFactory.Create(plain);
            var encrypted = plainString.DiskValue(true);
            Assert.IsNotNull(encrypted);

            // Read back
            var log = new Mock.Services.LogService(false);
            var readBack = psFactory.Parse(encrypted ?? "");
            Assert.AreEqual(plain, readBack.Value);

            // Turn off encryption
            var turnOff = psFactory.Parse(encrypted ?? "");
            var turnOffValue = turnOff.DiskValue(false);
            Assert.IsNotNull(turnOffValue);

            // Read back turned off value
            var readBack2 = psFactory.Parse(turnOffValue ?? "");
            Assert.AreEqual(readBack2.Value, plain);
        }
    }
}
