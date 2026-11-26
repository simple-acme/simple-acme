using Autofac;
using Autofac.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Configuration.Settings;
using PKISharp.WACS.Configuration.Settings.Types;
using PKISharp.WACS.Configuration.Settings.Types.Installation;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.DomainObjects.Identifiers;
using PKISharp.WACS.Plugins.InstallationPlugins;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.StorePlugins;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns;
using PKISharp.WACS.Plugins.ValidationPlugins.Http;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Encryption;
using PKISharp.WACS.Services.Serialization;
using PKISharp.WACS.UnitTests.Mock;
using PKISharp.WACS.UnitTests.Mock.Clients;
using PKISharp.WACS.UnitTests.Mock.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading.Tasks;

namespace PKISharp.WACS.UnitTests.Tests.InstallationPluginTests
{
    [OSCondition(OperatingSystems.Windows)]
    [SupportedOSPlatform("windows")]
    [TestClass]
    public class IISTests
    {
        private readonly Mock.Services.LogService log;
        private readonly MockIISClient iis;
        private readonly PluginService plugins;

        public IISTests()
        {
            log = new Mock.Services.LogService(false);
            iis = new MockIISClient(log);
            plugins = new PluginService(log, new MockAssemblyService(log));
        }

        private IISOptionsFactory CreateFactory(params string[] args)
        {
            var parser = new ArgumentsParser(log, new MockAssemblyService(log), args);
            var input = new Mock.Services.InputService([]);
            var secretService = new SecretServiceManager(MockContainer.TestScope(), input, plugins, log);
            var psFactory = new ProtectedStringFactory(new MockProtector(), log);
            var argsInput = new ArgumentsInputService(log, parser, input, secretService, psFactory);
            var target = new Target(new DnsIdentifier("test.example.com")) { Parts = { new TargetPart([new DnsIdentifier("test.example.com")]) { SiteId = 1, SiteType = IISSiteType.Web } } };
            return new IISOptionsFactory(iis, target, argsInput);
        }

        [TestMethod]
        public async Task BindingFlagsOmitted()
        {
            var factory = CreateFactory();
            var options = await factory.Default();
            Assert.IsNotNull(options);
            Assert.IsNull(options.BindingFlags);
        }

        [TestMethod]
        [DataRow("DisableHTTP2", new[] { "disablehttp2" })]
        [DataRow("disablehttp2", new[] { "disablehttp2" })]
        [DataRow("DisableHTTP2,DisableTLS13", new[] { "disablehttp2", "disabletls13" })]
        [DataRow("DisableHTTP2, DisableTLS13, NegotiateClientCert", new[] { "disablehttp2", "disabletls13", "negotiateclientcert" })]
        [DataRow("None", new[] { "none" })]
        public async Task BindingFlagsValid(string flagArg, string[] expected)
        {
            var factory = CreateFactory("--iisbindingflags", flagArg);
            var options = await factory.Default();
            Assert.IsNotNull(options);
            Assert.IsNotNull(options.BindingFlags);
            CollectionAssert.AreEqual(expected, options.BindingFlags);
        }

        [TestMethod]
        [DataRow("NotAFlag")]
        [DataRow("Sni")]
        [DataRow("CentralCertStore")]
        [DataRow("OptionalFlags")]
        [DataRow("IIS10_Flags")]
        [DataRow("4")]
        [DataRow(",")]
        [DataRow("DisableHTTP2,NotAFlag")]
        public async Task BindingFlagsInvalid(string flagArg)
        {
            var factory = CreateFactory("--iisbindingflags", flagArg);
            _ = await Assert.ThrowsAsync<Exception>(factory.Default);
        }

        [TestMethod]
        public void DescribeBindingFlags()
        {
            var factory = CreateFactory();
            var withFlags = new IISOptions
            {
                BindingFlags = ["disablehttp2", "disabletls13"]
            };
            var described = factory.Describe(withFlags).ToList();
            Assert.IsTrue(described.Any(d => d.Item1.ArgumentName == "iisbindingflags" && d.Item2 is List<string> l && l.SequenceEqual(["disablehttp2", "disabletls13"])));

            var withoutFlags = new IISOptions();
            var describedEmpty = factory.Describe(withoutFlags).ToList();
            Assert.IsTrue(describedEmpty.Any(d => d.Item1.ArgumentName == "iisbindingflags" && d.Item2 == null));
        }

        [TestMethod]
        public void JsonRoundTrip()
        {
            var builder = new ContainerBuilder();
            var assembly = new AssemblyService(log);
            var plugin = new PluginService(log, assembly);
            _ = builder.RegisterType<MockSettingsService>().As<ISettings>().SingleInstance();
            _ = builder.RegisterInstance(assembly).As<AssemblyService>().SingleInstance();
            _ = builder.RegisterInstance(log).As<ILogService>().SingleInstance();
            _ = builder.RegisterType<FtpJson>();
            _ = builder.RegisterType<AcmeJson>();
            _ = builder.RegisterType<ProtectedStringFactory>().SingleInstance();
            _ = builder.RegisterType<MockProtector>().As<IProtector>().SingleInstance();
            _ = builder.RegisterInstance(plugin).As<IPluginService>().SingleInstance();
            _ = builder.Register(c => (ISharingLifetimeScope)c.Resolve<ILifetimeScope>()).As<ISharingLifetimeScope>().ExternallyOwned();
            WacsJson.Configure(builder);
            using var scope = builder.Build();
            var wacsJson = scope.Resolve<WacsJson>();
            var original = new Renewal
            {
                Id = "test-iis-flags",
                TargetPluginOptions = new Plugins.TargetPlugins.ManualOptions { CommonName = "test.example.com", AlternativeNames = ["test.example.com"] },
                ValidationPluginOptions = new Plugins.ValidationPlugins.Any.NullOptions(),
                InstallationPluginOptions =
                [
                    new IISOptions
                    {
                        SiteId = 1,
                        BindingFlags = ["disablehttp2", "disablequic"]
                    }
                ]
            };
            var json = JsonSerializer.Serialize(original, wacsJson.Renewal);
            var roundTripped = JsonSerializer.Deserialize(json, wacsJson.Renewal);
            Assert.IsNotNull(roundTripped);
            var parsedOptions = roundTripped.InstallationPluginOptions.FirstOrDefault() as IISOptions;
            Assert.IsNotNull(parsedOptions);
            Assert.IsNotNull(parsedOptions.BindingFlags);
            CollectionAssert.AreEqual(new[] { "disablehttp2", "disablequic" }, parsedOptions.BindingFlags);
        }

        [TestMethod]
        public async Task InstallAppliesPerRenewalBindingFlags()
        {
            var settings = new MockSettingsService(new Settings
            {
                Installation = new InstallationSettings
                {
                    IIS = new IISSettings { BindingFlags = ["DisableLegacyTLS"] }
                }
            });
            var target = new Target(new DnsIdentifier("test.example.com"))
            {
                Parts =
                {
                    new TargetPart([new DnsIdentifier("test.example.com")])
                    {
                        SiteId = 1,
                        SiteType = IISSiteType.Web
                    }
                }
            };
            var options = new IISOptions
            {
                SiteId = 1,
                BindingFlags = ["disablehttp2", "disabletls13"]
            };
            IInstallationPlugin installer = new IIS(options, iis, settings, log, target);
            var storeInfo = new Dictionary<Type, StoreInfo>
            {
                { typeof(CertificateStore), new StoreInfo { Path = "WebHosting" } }
            };
            var cert = new MockCertificateInfo([0x01, 0x02], [new DnsIdentifier("test.example.com")]);
            var result = await installer.Install(storeInfo, cert, null);
            Assert.IsTrue(result);

            var site = iis.GetSite(1);
            var httpsBinding = site.Bindings.FirstOrDefault(b => b.Protocol == "https" && b.Host == HostName.Parse("test.example.com"));
            Assert.IsNotNull(httpsBinding);
            Assert.AreEqual(SSLFlags.Sni | SSLFlags.DisableHTTP2 | SSLFlags.DisableTLS13, httpsBinding.SSLFlags);
        }

        [TestMethod]
        public async Task InstallNoneOverridesSettingsBindingFlags()
        {
            var settings = new MockSettingsService(new Settings
            {
                Installation = new InstallationSettings
                {
                    IIS = new IISSettings { BindingFlags = ["DisableHTTP2"] }
                }
            });
            var target = new Target(new DnsIdentifier("test.example.com"))
            {
                Parts =
                {
                    new TargetPart([new DnsIdentifier("test.example.com")])
                    {
                        SiteId = 1,
                        SiteType = IISSiteType.Web
                    }
                }
            };
            var options = new IISOptions
            {
                SiteId = 1,
                BindingFlags = ["none"]
            };
            IInstallationPlugin installer = new IIS(options, iis, settings, log, target);
            var storeInfo = new Dictionary<Type, StoreInfo>
            {
                { typeof(CertificateStore), new StoreInfo { Path = "WebHosting" } }
            };
            var cert = new MockCertificateInfo([0x01, 0x02], [new DnsIdentifier("test.example.com")]);
            var result = await installer.Install(storeInfo, cert, null);
            Assert.IsTrue(result);

            var site = iis.GetSite(1);
            var httpsBinding = site.Bindings.FirstOrDefault(b => b.Protocol == "https" && b.Host == HostName.Parse("test.example.com"));
            Assert.IsNotNull(httpsBinding);
            Assert.AreEqual(SSLFlags.Sni, httpsBinding.SSLFlags);
        }

        [TestMethod]
        public async Task InstallFallsBackToSettingsWhenOptionOmitted()
        {
            var settings = new MockSettingsService(new Settings
            {
                Installation = new InstallationSettings
                {
                    IIS = new IISSettings { BindingFlags = ["DisableHTTP2"] }
                }
            });
            var target = new Target(new DnsIdentifier("test.example.com"))
            {
                Parts =
                {
                    new TargetPart([new DnsIdentifier("test.example.com")])
                    {
                        SiteId = 1,
                        SiteType = IISSiteType.Web
                    }
                }
            };
            var options = new IISOptions { SiteId = 1 };
            IInstallationPlugin installer = new IIS(options, iis, settings, log, target);
            var storeInfo = new Dictionary<Type, StoreInfo>
            {
                { typeof(CertificateStore), new StoreInfo { Path = "WebHosting" } }
            };
            var cert = new MockCertificateInfo([0x01, 0x02], [new DnsIdentifier("test.example.com")]);
            var result = await installer.Install(storeInfo, cert, null);
            Assert.IsTrue(result);

            var site = iis.GetSite(1);
            var httpsBinding = site.Bindings.FirstOrDefault(b => b.Protocol == "https" && b.Host == HostName.Parse("test.example.com"));
            Assert.IsNotNull(httpsBinding);
            Assert.AreEqual(SSLFlags.Sni | SSLFlags.DisableHTTP2, httpsBinding.SSLFlags);
        }

        private sealed class MockCertificateInfo(byte[] hash, IEnumerable<Identifier> sanNames) : ICertificateInfo
        {
            public PfxWrapper Collection => throw new NotImplementedException();
            public X509Certificate Certificate => throw new NotImplementedException();
            public AsymmetricKeyParameter? PrivateKey => null;
            public IEnumerable<X509Certificate> Chain => [];
            public DnsIdentifier? CommonName => sanNames.OfType<DnsIdentifier>().FirstOrDefault();
            public IEnumerable<Identifier> SanNames => sanNames;
            public string FriendlyName => "Mock";
            public byte[] GetHash() => hash;
            public string Thumbprint => Convert.ToHexString(hash);
        }
    }
}
