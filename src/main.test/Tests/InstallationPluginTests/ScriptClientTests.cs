using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Clients;
using PKISharp.WACS.Services;
using PKISharp.WACS.UnitTests.Mock;
using PKISharp.WACS.UnitTests.Mock.Services;
using System.Collections.Generic;
using System.IO;

namespace PKISharp.WACS.UnitTests.Tests.InstallationPluginTests
{
    [TestClass]
    public class ScriptClientTests
    {
        private readonly Mock.Services.LogService log;
        private FileInfo? psExit;

        public ScriptClientTests()
        {
            log = new Mock.Services.LogService(false);
        }

        [TestMethod]
        [DataRow(0, true)]
        [DataRow(-1, false)]
        [DataRow(1, false)]
        public void TestEnvironmentExit(int exit, bool expectedSuccess)
        {
            var mock = MockContainer.TestScope();
            var settings = new MockSettingsService();
            var tempPath = Infrastructure.Directory.Temp();
            psExit = new FileInfo(tempPath.FullName + "\\exit.ps1");
            File.WriteAllText(psExit.FullName, $"[Environment]::Exit({exit})");
            var sc = new Clients.ScriptClient(log, settings);
            var success = sc.RunScript(psExit.FullName, "", "").Result.Success;
            Assert.AreEqual(expectedSuccess, success);
        }

        [TestMethod]
        [DataRow(0, true)]
        [DataRow(-1, false)]
        [DataRow(1, false)]
        public void TestExit(int exit, bool expectedSuccess)
        {
            var mock = MockContainer.TestScope();
            var settings = new MockSettingsService();
            var tempPath = Infrastructure.Directory.Temp();
            psExit = new FileInfo(tempPath.FullName + "\\exit.ps1");
            File.WriteAllText(psExit.FullName, $"exit {exit}");
            var sc = new Clients.ScriptClient(log, settings);
            var success = sc.RunScript(psExit.FullName, "", "").Result.Success;
            Assert.AreEqual(expectedSuccess, success);
        }

        [TestMethod]
        public void TestException()
        {
            var mock = MockContainer.TestScope();
            var settings = new MockSettingsService();
            var tempPath = Infrastructure.Directory.Temp();
            psExit = new FileInfo(tempPath.FullName + "\\exit.ps1");
            File.WriteAllText(psExit.FullName, $"throw 'error'");
            var sc = new Clients.ScriptClient(log, settings);
            var success = sc.RunScript(psExit.FullName, "", "").Result.Success;
            Assert.AreEqual(false, success);
        }

        [TestMethod]
        public void TestExceptionCatch()
        {
            var mock = MockContainer.TestScope();
            var settings = new MockSettingsService();
            var tempPath = Infrastructure.Directory.Temp();
            psExit = new FileInfo(tempPath.FullName + "\\exit.ps1");
            File.WriteAllText(psExit.FullName, $"try {{ throw 'error' }} catch {{ }}");
            var sc = new Clients.ScriptClient(log, settings);
            var success = sc.RunScript(psExit.FullName, "", "").Result.Success;
            Assert.AreEqual(true, success);
        }

        [TestMethod]
        [DataRow("{aaa}", "bbb")]
        [DataRow("{AAA}", "bbb")]
        [DataRow("{AaA}", "bbb")]
        [DataRow("{aaa}{aaa}{aaa}", "bbbbbbbbb")]
        [DataRow("{a}{a}{a}", "bla\\bla\\blabla\\bla\\blabla\\bla\\bla")]
        [DataRow("{aaa} {aaa}", "bbb bbb")]
        [DataRow(" {aaa} {aaa} ", " bbb bbb ")]
        [DataRow(" {aaaaa} {aaa} ", " {aaaaa} bbb ")]
        [DataRow("{vault://mock/key1}", "secret1")]
        [DataRow("{vault://mock/key2}", "secret2")]
        [DataRow("{vault://mock/key1}", "{vault://mock/key1}", true)]
        [DataRow("--secret={vault://mock/key2}&bla=1", "--secret=secret2&bla=1")]
        [DataRow("{vault://mock/key3}", "{vault://mock/key3}")]
        public void TestReplace(string original, string expected, bool censor = false)
        {
            var mock = MockContainer.TestScope();
            var replaced = ScriptClient.ReplaceTokens(original, new Dictionary<string, string?> { { "aaa", "bbb" }, { "a", "bla\\bla\\bla" } }, mock.Resolve<SecretServiceManager>(), censor).Result;
            Assert.AreEqual(expected, replaced);
        }
    }
}
