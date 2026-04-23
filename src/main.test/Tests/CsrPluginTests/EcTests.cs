using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Configuration.Settings;
using PKISharp.WACS.Configuration.Settings.Types;
using PKISharp.WACS.Configuration.Settings.Types.Csr;
using PKISharp.WACS.Plugins.CsrPlugins;
using PKISharp.WACS.UnitTests.Mock.Services;
using System.Reflection;

namespace PKISharp.WACS.UnitTests.Tests.CsrPluginTests
{
    [TestClass]
    public class EcTests
    {
        [TestMethod]
        public void GetEcCurve_UsesConfiguredCurve_WhenValid()
        {
            var settings = new MockSettingsService(new Settings
            {
                Csr = new CsrSettings
                {
                    Ec = new EcSettings
                    {
                        CurveName = "secp256r1"
                    }
                }
            });
            var plugin = new Ec(new LogService(false), settings, new EcOptions());
            var method = typeof(Ec).GetMethod("GetEcCurve", BindingFlags.Instance | BindingFlags.NonPublic);
            var result = method?.Invoke(plugin, null) as string;
            Assert.AreEqual("secp256r1", result);
        }

        [TestMethod]
        [DataRow("not-a-curve")]
        [DataRow("")]
        [DataRow(null)]
        public void GetEcCurve_FallsBackToDefault_WhenInvalidOrMissing(string? curve)
        {
            var settings = new MockSettingsService(new Settings
            {
                Csr = new CsrSettings
                {
                    Ec = new EcSettings
                    {
                        CurveName = curve
                    }
                }
            });
            var plugin = new Ec(new LogService(false), settings, new EcOptions());
            var method = typeof(Ec).GetMethod("GetEcCurve", BindingFlags.Instance | BindingFlags.NonPublic);
            var result = method?.Invoke(plugin, null) as string;
            Assert.AreEqual("secp384r1", result);
        }
    }
}
