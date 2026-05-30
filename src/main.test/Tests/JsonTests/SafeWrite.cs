using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Tls.Crypto;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.UnitTests.Mock;
using System;
using System.IO;

namespace PKISharp.WACS.UnitTests.Tests.JsonTests
{
    [TestClass]
    public class SafeWriteTests
    {
        
        [TestInitialize]
        public void Init()
        {
            var tempPath = Infrastructure.Directory.Temp();
            var file = new FileInfo(tempPath.FullName + "\\a.json");
            var newFile = new FileInfo(tempPath.FullName + "\\a.json.new");
            var previous = new FileInfo(tempPath.FullName + "\\a.json.previous");
            if (file.Exists) file.Delete();
            if (newFile.Exists) newFile.Delete();
            if (previous.Exists) previous.Delete();
        }

        [TestMethod]
        [DataRow("a", "b")]
        [DataRow("aaaa", "b")]
        [DataRow("a", "bbbb")]
        [DataRow("\na\n\n\t\n\ra", "b\n\n\t\nb\t\t\n\r")]
        [DataRow("漢字𡨸漢", "한자\t😀")]
        public void Regular(string initial, string updated)
        {
            var tempPath = Infrastructure.Directory.Temp();
            var file = new FileInfo(tempPath.FullName + "\\a.json");
            file.SafeWrite(initial).Wait(TestContext.CancellationTokenSource.Token);
            Assert.AreEqual(initial, File.ReadAllText(file.FullName));
            file.SafeWrite(updated).Wait(TestContext.CancellationTokenSource.Token);
            Assert.AreEqual(updated, File.ReadAllText(file.FullName));
        }

        [TestMethod]
        [DataRow("a")]
        public void NewExists(string initial)
        {
            var tempPath = Infrastructure.Directory.Temp();
            var file = new FileInfo(tempPath.FullName + "\\a.json");
            var newFile = new FileInfo(tempPath.FullName + "\\a.json.new");
            using var x = newFile.Create();
            x.Dispose();
            Assert.ThrowsAsync<Exception>(() => file.SafeWrite(initial));
        }

        [TestMethod]
        [DataRow("a")]
        public void PreviousExists(string initial)
        {
            var tempPath = Infrastructure.Directory.Temp();
            var file = new FileInfo(tempPath.FullName + "\\a.json");
            var previous = new FileInfo(tempPath.FullName + "\\a.json.previous");
            using var x = previous.Create();
            x.Dispose();
            Assert.ThrowsAsync<Exception>(() => file.SafeWrite(initial));
        }

        [TestMethod]
        [DataRow("a")]
        public void NewAndPreviousExists(string initial)
        {
        
            var tempPath = Infrastructure.Directory.Temp();
            var file = new FileInfo(tempPath.FullName + "\\a.json");
            var newFile = new FileInfo(tempPath.FullName + "\\a.json.new");
            var previous = new FileInfo(tempPath.FullName + "\\a.json.previous");
            using var x = previous.Create();
            x.Dispose();
            using var y = newFile.Create();
            y.Dispose();
            Assert.ThrowsAsync<Exception>(() => file.SafeWrite(initial));
        }

        public TestContext TestContext { get; set; } = null!;
    }
}
