﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.TargetPlugins;
using PKISharp.WACS.Services;
using PKISharp.WACS.UnitTests.Mock;
using PKISharp.WACS.UnitTests.Mock.Services;
using System.Linq;
using System.Runtime.Versioning;

namespace PKISharp.WACS.UnitTests.Tests.TargetPluginTests
{
    [SupportedOSPlatform("windows")]
    [TestClass]
    public class IISSiteTests
    {
        private readonly ILogService log;
        private readonly IIISClient iis;
        private readonly IISHelper helper;
        private readonly IPluginService plugins;
        private readonly DomainParseService domainParse;
        private readonly TargetValidator validator;

        public IISSiteTests()
        {
            log = new Mock.Services.LogService(false);
            iis = new Mock.Clients.MockIISClient(log);
            var settings = new MockSettingsService();
            var proxy = new Mock.Services.ProxyService();
            validator = new TargetValidator(log, settings);
            domainParse = new DomainParseService(log, proxy, settings);
            helper = new IISHelper(log, iis, domainParse);
            plugins = new PluginService(log, new MockAssemblyService(log));
        }

        private IISOptions? Options(string commandLine)
        {
            var optionsParser = new ArgumentsParser(log, new MockAssemblyService(log), commandLine.Split(' '));
            var input = new Mock.Services.InputService([]);
            var secretService = new SecretServiceManager(MockContainer.TestScope(), input, plugins, log);
            var argsInput = new ArgumentsInputService(log, optionsParser, input, secretService);
            var args = new MainArguments();
            var x = new IISOptionsFactory(log, helper, args, argsInput);
            return x.Default().Result;
        }

        private Target? Target(IISOptions options)
        {
            var plugin = new IIS(log, helper, options);
            return plugin.Generate().Result;
        }

        [TestMethod]
        public void Regular()
        {
            var siteId = 1;
            var site = iis.GetSite(siteId);
            var options = Options($"--siteid {siteId}");
            Assert.IsNotNull(options);
            if (options != null)
            {
                Assert.IsNotNull(options.IncludeSiteIds);
                if (options.IncludeSiteIds != null)
                {
                    Assert.HasCount(1, options.IncludeSiteIds);
                    Assert.Contains(1, options.IncludeSiteIds);
                    Assert.IsNull(options.CommonName);
                    Assert.IsNull(options.ExcludeHosts);
                    var target = Target(options);
                    Assert.IsNotNull(target);
                    Assert.IsTrue(validator.IsValid(target));
                    Assert.AreEqual(target.CommonName?.Value, site.Bindings.First().Host); // First binding
                    Assert.IsTrue(target.IIS);
                    Assert.HasCount(1, target.Parts);
                    Assert.AreEqual(target.Parts.First().SiteId, siteId);
                    Assert.AreEqual(target.Parts.First().Identifiers.Count, site.Bindings.Count());
                    Assert.IsTrue(target.Parts.First().Identifiers.All(x => site.Bindings.Any(b => b.Host == x.Value)));
                }
            }
        }

        [TestMethod]
        public void CommonNameUni()
        {
            var siteId = 1;
            var commonName = "经/已經.example.com";
            _ = iis.GetSite(siteId);
            var options = Options($"--siteid {siteId} --commonname {commonName}");
            Assert.IsNotNull(options); 
            if (options != null)
            {
                Assert.AreEqual(options.IncludeSiteIds?.FirstOrDefault(), siteId);
                Assert.AreEqual(options.CommonName, commonName);
                Assert.IsNull(options.ExcludeHosts);
                var target = Target(options);
                Assert.IsNotNull(target);
                Assert.IsTrue(validator.IsValid(target));
                Assert.AreEqual(target.CommonName?.Value, commonName); // First binding
            }
        }


        [TestMethod]
        public void CommonNamePuny()
        {
            var siteId = 1;
            var punyHost = "xn--/-9b3b774gbbb.example.com";
            var uniHost = "经/已經.example.com";
            var options = Options($"--siteid {siteId} --commonname {punyHost}");
            Assert.IsNotNull(options);
            if (options != null)
            {
                Assert.AreEqual(options.IncludeSiteIds?.FirstOrDefault(), siteId);
                Assert.AreEqual(options.CommonName, uniHost);
                Assert.IsNull(options.ExcludeHosts);
                var target = Target(options);
                Assert.IsNotNull(target);
                Assert.IsTrue(validator.IsValid(target));
                Assert.AreEqual(target.CommonName?.Value, uniHost); // First binding
            }
        }

        [TestMethod]
        public void ExcludeBindings()
        {
            var siteId = 1;
            var site = iis.GetSite(siteId);
            var options = Options($"--siteid {siteId} --excludebindings test.example.com,four.example.com");
            Assert.IsNotNull(options);
            if (options != null)
            {
                Assert.AreEqual(options.IncludeSiteIds?.FirstOrDefault(), siteId);
                Assert.IsNotNull(options.ExcludeHosts);
                Assert.AreEqual(options.ExcludeHosts?.Count, site.Bindings.Count() - 2);
                var target = Target(options);
                Assert.IsNotNull(target);
                Assert.IsTrue(validator.IsValid(target));
                Assert.IsFalse(target.Parts.First().Identifiers.Any(x => x.Value == "test.example.com"));
                Assert.IsFalse(target.Parts.First().Identifiers.Any(x => x.Value == "four.example.com"));
                Assert.AreEqual("alt.example.com", target.CommonName?.Value); // 2nd binding, first is excluded
            }
        }

        [TestMethod]
        public void ExcludeBindingsPuny()
        {
            var siteId = 1;
            var options = Options($"--siteid {siteId} --excludebindings xn--/-9b3b774gbbb.example.com");
            Assert.IsNotNull(options);
            if (options != null)
            {
                Assert.AreEqual(options.IncludeSiteIds?.FirstOrDefault(), siteId);
                Assert.IsNotNull(options.ExcludeHosts);
                if (options.ExcludeHosts != null)
                {
                    Assert.HasCount(1, options.ExcludeHosts);
                    Assert.AreEqual("经/已經.example.com", options.ExcludeHosts.First());
                }
            }
        }

        [TestMethod]
        public void CommonNameExcluded()
        {
            var siteId = 1;
            var options = Options($"--siteid {siteId} --commonname test.example.com --excludebindings test.example.com,four.example.com");
            Assert.IsNull(options);
        }

        [TestMethod]
        public void CommonNameExcludedAfter()
        {
            var siteId = 1;
            var site = iis.GetSite(siteId);
            var options = new IISSiteOptions() { SiteId = siteId, CommonName = "missing.example.com" };
            var target = Target(options);
            Assert.IsNotNull(target);
            Assert.IsTrue(validator.IsValid(target));
            Assert.AreEqual(target.CommonName?.Value, site.Bindings.First().Host);
        }

        [TestMethod]
        public void MissingSiteConfig()
        {
            var options = Options($"--siteid 999");
            Assert.IsNull(options);
        }

        [TestMethod]
        public void MissingSiteExecution()
        {
            var options = new IISSiteOptions()
            {
                SiteId = 999
            };
            var target = Target(options);
            Assert.IsNull(target);
        }

        [TestMethod]
        public void NoOptions()
        {
            var options = Options($"");
            Assert.IsNull(options);
        }

        [TestMethod]
        public void IllegalSite()
        {
            var options = Options($"--siteid ab");
            Assert.IsNull(options);
        }
    }
}