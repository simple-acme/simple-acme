using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Configuration.Settings;
using PKISharp.WACS.Configuration.Settings.Types;
using PKISharp.WACS.Configuration.Settings.Types.Notification;
using PKISharp.WACS.Services;
using System;

namespace PKISharp.WACS.UnitTests.Tests.SettingsTests
{
    [TestClass]
    public class PublicSuffixListTests
    {
        [TestMethod]
        [DataRow("host01.sub._acme.example.com", "example.com")]
        [DataRow("host01.sub.acme.example.com", "example.com")]
        [DataRow("sub._acme.example.com", "example.com")]
        [DataRow("_acme.example.com", "example.com")]
        [DataRow("example.com", "example.com")]
        [DataRow("eXaMplE.com", "example.com")]
        [DataRow("frisör.com", "frisör.com")]
        [DataRow("xn--frisr-mua.com", "xn--frisr-mua.com")]
        [DataRow("XN--frisr-mua.com", "xn--frisr-mua.com")]
        public void Nager(string? full, string? registerable)
        {
            var log = new Mock.Services.LogService();
            var proxy = new Mock.Services.ProxyService();
            var settings = new Mock.Services.MockSettingsService();
            var x = new DomainParseService(log, proxy, settings);
            x.Initialize().Wait();
            var ret = x.GetRegisterableDomain(full!);
            Assert.AreEqual(registerable, ret);
        }

        [TestMethod]
        public void Basic()
        {
            var globalSettings = new Settings
            {
                Acme = new AcmeSettings()
                {
                    PublicSuffixListUri = "https://publicsuffix.org/list/public_suffix_list.dat"
                }
            };
            var x = new InheritSettings(globalSettings);
            Assert.AreEqual(new Uri("https://publicsuffix.org/list/public_suffix_list.dat"), x.Acme.PublicSuffixListUri);
        }

        [TestMethod]
        public void Specific()
        {
            var globalSettings = new Settings
            {
                Acme = new AcmeSettings()
                {
                    PublicSuffixListUri = "https://localhost:4943/public_suffix_list.dat"
                }
            };
            var x = new InheritSettings(globalSettings);
            Assert.AreEqual(new Uri("https://localhost:4943/public_suffix_list.dat"), x.Acme.PublicSuffixListUri);
        }

        [TestMethod]
        public void Default()
        {
            var globalSettings = new Settings();
            var x = new InheritSettings(globalSettings);
            Assert.AreEqual(new Uri("https://publicsuffix.org/list/public_suffix_list.dat"), x.Acme.PublicSuffixListUri);
        }

        [TestMethod]
        public void Empty()
        {
            var globalSettings = new Settings
            {
                Acme = new AcmeSettings()
                {
                    PublicSuffixListUri = ""
                }
            };
            var x = new InheritSettings(globalSettings);
            Assert.IsNull(x.Acme.PublicSuffixListUri);
        }

        [TestMethod]
        public void OverruledEmptyVale()
        {
            var globalSettings = new Settings
            {
                Acme = new AcmeSettings()
                {
                    PublicSuffixListUri = ""
                }
            };
            var localSettings = new Settings
            {
                Acme = new AcmeSettings()
                {
                    PublicSuffixListUri = "https://publicsuffix.org/list/public_suffix_list.dat"
                }
            };
            var x = new InheritSettings(localSettings, globalSettings);
            Assert.AreEqual(new Uri("https://publicsuffix.org/list/public_suffix_list.dat"), x.Acme.PublicSuffixListUri);
        }

        [TestMethod]
        public void OverruledDefault()
        {
            var globalSettings = new Settings();
            var localSettings = new Settings
            {
                Acme = new AcmeSettings()
                {
                    PublicSuffixListUri = "https://localhost"
                }
            };
            var x = new InheritSettings(localSettings, globalSettings);
            Assert.AreEqual(new Uri("https://localhost"), x.Acme.PublicSuffixListUri);
        }

        [TestMethod]
        public void OverruledDefault2()
        {
            var localSettings = new Settings();
            var globalSettings = new Settings
            {
                Acme = new AcmeSettings()
                {
                    PublicSuffixListUri = "https://localhost"
                }
            };
            var x = new InheritSettings(localSettings, globalSettings);
            Assert.AreEqual(new Uri("https://localhost"), x.Acme.PublicSuffixListUri);
        }

        [TestMethod]
        public void OverruledDefault3()
        {
            var globalSettings = new Settings();
            var localSettings = new Settings
            {
                Acme = new AcmeSettings()
                {
                    PublicSuffixListUri = ""
                }
            };
            var x = new InheritSettings(localSettings, globalSettings); 
            Assert.IsNull(x.Acme.PublicSuffixListUri);
        }

        [TestMethod]
        public void ThreeLevel()
        {
            var globalSettings = new Settings();
            var localSettings = new Settings
            {
                Acme = new AcmeSettings()
                {
                    PublicSuffixListUri = "https://localhost"
                }
            };
            var renewallSettings = new Settings
            {
                Acme = new AcmeSettings()
                {
                    PublicSuffixListUri = ""
                }
            };
            var x = new InheritSettings(renewallSettings, localSettings, globalSettings); 
            Assert.IsNull(x.Acme.PublicSuffixListUri);
        }
    }

    [TestClass]
    public class EmailSettingsTests
    {
        [TestMethod]
        public void SmtpLocalDomainDefaultsToNull()
        {
            var settings = new InheritSettings(new Settings());

            Assert.IsNull(settings.Notification.Email!.SmtpLocalDomain);
        }

        [TestMethod]
        public void SmtpLocalDomainCanBeConfigured()
        {
            var globalSettings = new Settings
            {
                Notification = new NotificationSettings
                {
                    Email = new EmailSettings
                    {
                        SmtpLocalDomain = "mail.example.com"
                    }
                }
            };
            var settings = new InheritSettings(globalSettings);

            Assert.AreEqual("mail.example.com", settings.Notification.Email!.SmtpLocalDomain);
        }
    }
}
