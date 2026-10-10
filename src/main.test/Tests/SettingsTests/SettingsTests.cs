using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Configuration.Settings;
using PKISharp.WACS.Configuration.Settings.Types;
using PKISharp.WACS.Configuration.Settings.Types.Notification;
using System;

namespace PKISharp.WACS.UnitTests.Tests.SettingsTests
{
    [TestClass]
    public class PublicSuffixListTests
    {
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

    [TestClass]
    public class ValidationPortSettingsTests
    {
        [TestMethod]
        public void ValidationPortDefaultsToNull()
        {
            var settings = new InheritSettings(new Settings());

            Assert.IsNull(settings.Validation.ValidationPort);
        }

        [TestMethod]
        public void ValidationPortCanBeConfigured()
        {
            var globalSettings = new Settings
            {
                Validation = new ValidationSettings
                {
                    ValidationPort = 9040
                }
            };
            var settings = new InheritSettings(globalSettings);

            Assert.AreEqual(9040, settings.Validation.ValidationPort);
        }

        [TestMethod]
        public void ValidationPortCanBeOverruled()
        {
            var globalSettings = new Settings
            {
                Validation = new ValidationSettings
                {
                    ValidationPort = 9040
                }
            };
            var localSettings = new Settings
            {
                Validation = new ValidationSettings
                {
                    ValidationPort = 8080
                }
            };
            var settings = new InheritSettings(localSettings, globalSettings);

            Assert.AreEqual(8080, settings.Validation.ValidationPort);
        }
    }
}
