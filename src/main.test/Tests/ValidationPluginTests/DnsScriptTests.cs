using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.ValidationPlugins.Any;
using PKISharp.WACS.Services;
using PKISharp.WACS.UnitTests.Mock;
using PKISharp.WACS.UnitTests.Mock.Services;
using System.IO;

namespace PKISharp.WACS.UnitTests.Tests.ValidationPluginTests
{
    [TestClass]
    public class DnsScriptTests
    {
        private readonly ILogService log;
        private readonly IPluginService plugins;
        private readonly FileInfo commonScript;
        private readonly FileInfo deleteScript;
        private readonly FileInfo createScript;

        public DnsScriptTests()
        {
            log = new Mock.Services.LogService(false);
            plugins = new PluginService(log, new MockAssemblyService(log));
            var tempPath = Infrastructure.Directory.Temp();
            commonScript = new FileInfo(tempPath.FullName + "\\dns-common.bat");
            File.WriteAllText(commonScript.FullName, "");
            deleteScript = new FileInfo(tempPath.FullName + "\\dns-delete.bat");
            File.WriteAllText(deleteScript.FullName, "");
            createScript = new FileInfo(tempPath.FullName + "\\dns-create.bat");
            File.WriteAllText(createScript.FullName, "");
        }

        private ScriptOptions? Options(string commandLine)
        {
            var optionsParser = new ArgumentsParser(log, new MockAssemblyService(log), commandLine.Split(' '));
            var input = new Mock.Services.InputService([]);
            var secretService = new SecretServiceManager(MockContainer.TestScope(), input, plugins, log);
            var argsInput = new ArgumentsInputService(log, optionsParser, input, secretService);
            var x = new ScriptOptionsFactory(log, new MockSettingsService(), argsInput);
            return x.Default().Result;
        }

        [TestMethod]
        [DataRow("--validationmode DNS-01", null)]
        [DataRow("--validationmode HTTP-01", "http-01")]
        [DataRow("--validationmode dns-01", null)]
        [DataRow("--validationmode http-01", "http-01")]
        [DataRow("--validationmode nonsense", null)]
        [DataRow("", null)]
        public void Type(string arg1, string? expected)
        {
            var options = Options($"{arg1}--validationscript {commonScript.FullName}");
            Assert.IsNotNull(options);
            if (options != null)
            {
                Assert.AreEqual(expected, options.ChallengeType);
                Assert.AreEqual(options.Script, commonScript.FullName);
                Assert.IsNull(options.CreateScript, commonScript.FullName);
                Assert.IsNull(options.DeleteScript, commonScript.FullName);
            }
        }

        [TestMethod]
        [DataRow("--dnsscript")]
        [DataRow("--validationscript")]
        public void OnlyCommon(string arg1)
        {
            var options = Options($"{arg1} {commonScript.FullName}");
            Assert.IsNotNull(options);
            if (options != null)
            {
                Assert.AreEqual(options.Script, commonScript.FullName);
                Assert.IsNull(options.CreateScript, commonScript.FullName);
                Assert.IsNull(options.DeleteScript, commonScript.FullName);
            }
        }

        [TestMethod]
        public void None()
        {
            var options = Options($"");
            Assert.IsNull(options);
        }

        [TestMethod]
        [DataRow("--dnsdeletescript", "--dnscreatescript")]
        [DataRow("--validationcleanupscript", "--validationpreparescript")]
        public void AutoMerge(string arg1, string arg2)
        {
            var options = Options($"{arg1} {commonScript.FullName} {arg2} {commonScript.FullName}");
            Assert.IsNotNull(options);
            if (options != null)
            {
                Assert.AreEqual(options.Script, commonScript.FullName);
                Assert.IsNull(options.CreateScript, commonScript.FullName);
                Assert.IsNull(options.DeleteScript, commonScript.FullName);
            }
        }

        [TestMethod]
        [DataRow("--dnsdeletescript", "--dnscreatescript")]
        [DataRow("--validationcleanupscript", "--validationpreparescript")]
        public void Different(string arg1, string arg2)
        {
            var options = Options($"{arg1} {deleteScript.FullName} {arg2} {createScript.FullName}");
            Assert.IsNotNull(options); if (options != null)
            {
                Assert.IsNull(options.Script);
                Assert.AreEqual(options.CreateScript, createScript.FullName);
                Assert.AreEqual(options.DeleteScript, deleteScript.FullName);
            }
        }

        [TestMethod]
        [DataRow("--dnscreatescript")]
        [DataRow("--validationpreparescript")]
        public void CreateOnly(string arg1)
        {
            var options = Options($"{arg1} {createScript.FullName}");
            Assert.IsNotNull(options);
            if (options != null)
            {
                Assert.IsNull(options.Script);
                Assert.AreEqual(options.CreateScript, createScript.FullName);
                Assert.IsNull(options.DeleteScript, deleteScript.FullName); 
            }
        }

        [TestMethod]
        [DataRow("--dnscreatescript")]
        [DataRow("--validationpreparescript")]
        public void WrongPath(string arg1)
        {
            try
            {
                var options = Options($"{arg1} {createScript.FullName}error");
                Assert.Fail("Should have thrown exception");
            }
            catch
            {

            }

        }
    }
}