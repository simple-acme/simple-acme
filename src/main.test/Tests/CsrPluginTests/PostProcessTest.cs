using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.CsrPlugins;
using PKISharp.WACS.Services.Encryption;
using PKISharp.WACS.Services.Serialization;
using System;
using System.IO;
using System.Reflection;

namespace PKISharp.WACS.UnitTests.Tests.CsrPluginTests
{
    [TestClass]
    public class PostProcessTest
    {
        [TestMethod]
        [DataRow("working")]
        public void Convert(string name)
        {
            var log = new Mock.Services.LogService(false);
            var settings = new Mock.Services.MockSettingsService();
            var psFactory = new ProtectedStringFactory(new NullProtector(), log);
            _ = new Rsa(log, settings, psFactory, new RsaOptions());
            var data = Assembly.
                GetAssembly(typeof(PostProcessTest))!.
                GetManifestResourceStream($"PKISharp.WACS.UnitTests.Tests.CsrPluginTests.{name}.pfx") ?? 
                throw new Exception();
            var ms = new MemoryStream();
            data.CopyTo(ms);
            var bytes = ms.ToArray();
            var tempFile = Path.GetTempFileName();
            File.WriteAllBytes(tempFile, bytes);
            var fi = new FileInfo(tempFile);
            _ = new CertificateInfoCache(fi, "A8<TEpyPweWMO1m(");
            //_ = x.PostProcess(certInfo.Certificate).Result;
        }
    }
}
