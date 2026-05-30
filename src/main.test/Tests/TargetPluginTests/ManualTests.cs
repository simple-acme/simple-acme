using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.TargetPlugins;
using PKISharp.WACS.Services;
using PKISharp.WACS.UnitTests.Mock;
using PKISharp.WACS.UnitTests.Mock.Services;
using System;
using System.Linq;

namespace PKISharp.WACS.UnitTests.Tests.TargetPluginTests
{
    [TestClass]
    public class ManualTests
    {
        private readonly ILogService log;
        private readonly IPluginService plugins;
        private readonly TargetValidator validator;

        public ManualTests()
        {
            log = new Mock.Services.LogService(false);
            plugins = new PluginService(log, new MockAssemblyService(log));
            validator = new TargetValidator(log, new MockSettingsService());
        }

        private ManualOptions? Options(string commandLine)
        {
            var optionsParser = new ArgumentsParser(log, new MockAssemblyService(log), commandLine.Split(' '));
            var input = new Mock.Services.InputService([]);
            var secretService = new SecretServiceManager(MockContainer.TestScope(), input, plugins, log);
            var argsInput = new ArgumentsInputService(log, optionsParser, input, secretService);
            var x = new ManualOptionsFactory(argsInput);
            return x.Default().Result;
        }

        private static Target? Target(ManualOptions options)
        {
            var plugin = new Manual(options);
            return plugin.Generate().Result;
        }

        [TestMethod]
        public void Regular()
        {
            var options = Options($"--host a.example.com,b.example.com,c.example.com");
            Assert.IsNotNull(options);
            if (options != null)
            {
                Assert.AreEqual("a.example.com", options.CommonName);
                Assert.HasCount(3, options.AlternativeNames);
                var tar = Target(options);
                Assert.IsNotNull(tar);
                Assert.IsTrue(validator.IsValid(tar));
            }
        }

        [TestMethod]
        public void Puny()
        {
            var options = Options($"--host xn--/-9b3b774gbbb.example.com");
            Assert.IsNotNull(options);
            if (options != null)
            {
                Assert.AreEqual("经/已經.example.com", options.CommonName);
                Assert.AreEqual("经/已經.example.com", options.AlternativeNames.First());
                var tar = Target(options);
                Assert.IsNotNull(tar);
                Assert.IsTrue(validator.IsValid(tar));
            }
        }

        [TestMethod]
        public void PunyWildcard()
        {
            var options = Options($"--host *.xn--/-9b3b774gbbb.example.com");
            Assert.IsNotNull(options);
            if (options != null)
            {
                Assert.AreEqual("*.经/已經.example.com", options.CommonName);
                Assert.AreEqual("*.经/已經.example.com", options.AlternativeNames.First());
                var tar = Target(options);
                Assert.IsNotNull(tar);
                Assert.IsTrue(validator.IsValid(tar));
            }
        }

        [TestMethod]
        public void PunySubDomain()
        {
            var options = Options($"--host xn--/-9b3b774gbbb.xn--/-9b3b774gbbb.example.com");
            Assert.IsNotNull(options);
            if (options != null)
            {
                Assert.AreEqual("经/已經.经/已經.example.com", options.CommonName);
                Assert.AreEqual("经/已經.经/已經.example.com", options.AlternativeNames.First());
                var tar = Target(options);
                Assert.IsNotNull(tar);
                Assert.IsTrue(validator.IsValid(tar));
            }
        }

        [TestMethod]
        public void UniWildcard()
        {
            var options = Options($"--host *.经/已經.example.com");
            Assert.IsNotNull(options);
            if (options != null)
            {
                Assert.AreEqual("*.经/已經.example.com", options.CommonName);
                Assert.AreEqual("*.经/已經.example.com", options.AlternativeNames.First());
                var tar = Target(options);
                Assert.IsNotNull(tar);
                Assert.IsTrue(validator.IsValid(tar));
            }
        }

        [TestMethod]
        public void UniSubDomain()
        {
            var options = Options($"--host 经/已經.经/已經.example.com");
            Assert.IsNotNull(options);
            if (options != null)
            {
                Assert.AreEqual("经/已經.经/已經.example.com", options.CommonName);
                Assert.AreEqual("经/已經.经/已經.example.com", options.AlternativeNames.First());
                var tar = Target(options);
                Assert.IsNotNull(tar);
                Assert.IsTrue(validator.IsValid(tar));
            }
        }

        [TestMethod]
        public void IpAddress()
        {
            var options = Options($"--host abc.com,1.2.3.4");
            Assert.IsNotNull(options);
            if (options != null)
            {
                var tar = Target(options);
                Assert.IsNotNull(tar);
                Assert.IsTrue(validator.IsValid(tar));
                Assert.AreEqual("1.2.3.4", tar.Parts.First().Identifiers.OfType<IpIdentifier>().First().Value);
            }
        }

        [TestMethod]
        public void IpAddressCommon()
        {
            var options = Options($"--host 1.2.3.4,abc.com");
            Assert.IsNotNull(options);
            if (options != null)
            {
                var tar = Target(options);
                Assert.IsNotNull(tar);
                Assert.IsTrue(validator.IsValid(tar));
                Assert.AreEqual("abc.com", tar.CommonName?.Value);
            }
        }

        [TestMethod]
        public void CommonNameUni()
        {
            var options = Options($"--commonname common.example.com --host a.example.com,b.example.com,c.example.com");
            Assert.IsNotNull(options);
            if (options != null)
            {
                Assert.AreEqual("common.example.com", options.CommonName);
                Assert.HasCount(4, options.AlternativeNames);
                var tar = Target(options);
                Assert.IsNotNull(tar);
                Assert.IsTrue(validator.IsValid(tar));
            }
        }

        [TestMethod]
        public void CommonNamePuny()
        {
            var options = Options($"--commonname xn--/-9b3b774gbbb.example.com --host 经/已經.example.com,b.example.com,c.example.com");
            Assert.IsNotNull(options);
            if (options != null)
            {
                Assert.AreEqual("经/已經.example.com", options.CommonName);
                Assert.HasCount(3, options.AlternativeNames);
                var tar = Target(options);
                Assert.IsNotNull(tar);
                Assert.IsTrue(validator.IsValid(tar));
            }
        }

        [TestMethod]
        public void NoHost() => Assert.Throws<Exception>(() => Options($""));
    }
}