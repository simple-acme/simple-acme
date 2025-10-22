﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.UnitTests.Mock.Clients;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace PKISharp.WACS.UnitTests.Tests.BindingTests
{
    [TestClass]
    [SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments", Justification = "Not supported by decorators")]
    public class BindingTests
    {
        private readonly Mock.Services.LogService log;

        private const string DefaultIP = IISClient.DefaultBindingIp;
        private const string AltIP = "1.1.1.1";

        private const string DefaultStore = "My";
        private const string AltStore = "WebHosting";

        private const int DefaultPort = IISClient.DefaultBindingPort;
        private const int AltPort = 1234;

        private readonly byte[] newCert = [0x2];
        private readonly byte[] oldCert1 = [0x1];
        private readonly byte[] scopeCert = [0x0];

        private const string httpOnlyHost = "httponly.example.com";
        private const long httpOnlyId = 1;

        private const string regularHost = "regular.example.com";
        private const long regularId = 2;

        private const string outofscopeHost = "outofscope.example.com";
        private const long outofscopeId = 3;

        private const string inscopeHost = "inscope.example.com";
        private const long inscopeId = 4;

        private const long piramidId = 5;

        private const string sniTrapHost = "snitrap.example.com";
        private const long sniTrap1 = 6;
        private const long sniTrap2 = 7;

        public BindingTests() => log = new Mock.Services.LogService(false);

        [TestMethod]
        // Basic
        [DataRow(DefaultStore, DefaultIP, DefaultPort, SSLFlags.None, SSLFlags.Sni, 10)]
        // Alternative store
        [DataRow(AltStore, DefaultIP, DefaultPort, SSLFlags.None, SSLFlags.Sni, 10)]
        // Alternative IP
        [DataRow(DefaultStore, AltIP, DefaultPort, SSLFlags.None, SSLFlags.Sni, 10)]
        // Alternative port
        [DataRow(DefaultStore, DefaultIP, AltPort, SSLFlags.None, SSLFlags.Sni, 10)]
        // Alternative flags
        [DataRow(DefaultStore, DefaultIP, DefaultPort, SSLFlags.CentralCertStore, SSLFlags.Sni | SSLFlags.CentralCertStore, 10)]
        // Unsupported flags
        [DataRow(DefaultStore, DefaultIP, DefaultPort, SSLFlags.None, SSLFlags.None, 7)]
        [DataRow(DefaultStore, DefaultIP, DefaultPort, SSLFlags.Sni, SSLFlags.None, 7)]
        [DataRow(DefaultStore, DefaultIP, DefaultPort, SSLFlags.CentralCertStore, SSLFlags.None, 7)]
        public void AddNewSingle(string storeName, string bindingIp, int bindingPort, SSLFlags inputFlags, SSLFlags expectedFlags, int iisVersion)
        {
            var iis = new MockIISClient(log, iisVersion)
            {
                MockSites = [
                    new MockSite() {
                        Id = httpOnlyId,
                        Bindings = [
                            new() {
                                IP = "*",
                                Port = 80,
                                Host = httpOnlyHost,
                                Protocol = "http"
                            }
                        ]
                    }
                ]
            };
            var testHost = httpOnlyHost;
            var bindingOptions = new BindingOptions().
                WithSiteId(httpOnlyId).
                WithIP(bindingIp).
                WithPort(bindingPort).
                WithStore(storeName).
                WithFlags(inputFlags);

            if (!inputFlags.HasFlag(SSLFlags.CentralCertStore))
            {
                bindingOptions = bindingOptions.WithThumbprint(newCert);
            }

            var httpOnlySite = iis.GetSite(httpOnlyId);
            iis.UpdateHttpSite(new[] { new DnsIdentifier(testHost) }, bindingOptions, oldCert1);
            Assert.HasCount(2, httpOnlySite.Bindings);

            var newBinding = httpOnlySite.Bindings[1];
            Assert.AreEqual(testHost, newBinding.Host);
            Assert.AreEqual("https", newBinding.Protocol);
            Assert.AreEqual(storeName, newBinding.CertificateStoreName);
            if (!expectedFlags.HasFlag(SSLFlags.CentralCertStore) && iisVersion > 7)
            {
                Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(newCert, newBinding.CertificateHash));
            }
            Assert.AreEqual(bindingPort, newBinding.Port);
            Assert.AreEqual(bindingIp, newBinding.IP);
            Assert.AreEqual(expectedFlags, newBinding.SSLFlags);
        }

        [TestMethod]
        // Basic
        [DataRow(httpOnlyHost, httpOnlyHost, DefaultStore, DefaultIP, DefaultPort, SSLFlags.None, SSLFlags.Sni, 10)]
        // Alternative host
        [DataRow("*.example.com", httpOnlyHost, DefaultStore, DefaultIP, DefaultPort, SSLFlags.None, SSLFlags.Sni, 10)]
        [DataRow("*.example.com", "", DefaultStore, DefaultIP, DefaultPort, SSLFlags.None, SSLFlags.None, 10)]
        // Alternative store
        [DataRow(httpOnlyHost, httpOnlyHost, AltStore, DefaultIP, DefaultPort, SSLFlags.None, SSLFlags.Sni, 10)]
        // Alternative IP
        [DataRow(httpOnlyHost, httpOnlyHost, DefaultStore, AltIP, DefaultPort, SSLFlags.None, SSLFlags.Sni, 10)]
        // Alternative port
        [DataRow(httpOnlyHost, httpOnlyHost, DefaultStore, DefaultIP, AltPort, SSLFlags.None, SSLFlags.Sni, 10)]
        // Alternative flags
        [DataRow(httpOnlyHost, httpOnlyHost, DefaultStore, DefaultIP, DefaultPort, SSLFlags.CentralCertStore, SSLFlags.Sni | SSLFlags.CentralCertStore, 10)]
        // Unsupported flags
        [DataRow(httpOnlyHost, httpOnlyHost, DefaultStore, DefaultIP, DefaultPort, SSLFlags.None, SSLFlags.None, 7)]
        [DataRow(httpOnlyHost, httpOnlyHost, DefaultStore, DefaultIP, DefaultPort, SSLFlags.Sni, SSLFlags.None, 7)]
        [DataRow(httpOnlyHost, httpOnlyHost, DefaultStore, DefaultIP, DefaultPort, SSLFlags.CentralCertStore, SSLFlags.None, 7)]
        public void AddNewMulti(string newHost, string existingHost, string storeName, string bindingIp, int bindingPort, SSLFlags inputFlags, SSLFlags expectedFlags, int iisVersion)
        {
            var iis = new MockIISClient(log, iisVersion)
            {
                MockSites = [
                    new() {
                        Id = httpOnlyId,
                        Bindings = [
                            new() {
                                IP = "1.1.1.1",
                                Port = 80,
                                Host = existingHost,
                                Protocol = "http"
                            },
                            new() {
                                IP = "1.1.1.1",
                                Port = 81,
                                Host = existingHost,
                                Protocol = "http"
                            },
                            new() {
                                IP = "1234:1235:1235",
                                Port = 80,
                                Host = existingHost,
                                Protocol = "http"
                            }
                        ]
                    }
                ]
            };
            var bindingOptions = new BindingOptions().
                WithSiteId(httpOnlyId).
                WithIP(bindingIp).
                WithPort(bindingPort).
                WithStore(storeName).
                WithFlags(inputFlags).
                WithThumbprint(newCert);

            var httpOnlySite = iis.GetSite(httpOnlyId);
            var existingBindings = httpOnlySite.Bindings.ToList();
            var expectedNew = existingBindings.Select(x => x.IP + x.Host).Distinct().Count();
            if (bindingIp != DefaultIP)
            {
                expectedNew = 1;
            }
       
            iis.UpdateHttpSite(new[] { new DnsIdentifier(newHost) }, bindingOptions, oldCert1);
        
            Assert.AreEqual(existingBindings.Count + expectedNew, httpOnlySite.Bindings.Count);

            var newBindings = httpOnlySite.Bindings.Except(existingBindings);
            _ = newBindings.All(newBinding =>
              {
                  Assert.AreEqual(existingHost, newBinding.Host);
                  Assert.AreEqual("https", newBinding.Protocol);
                  Assert.AreEqual(storeName, newBinding.CertificateStoreName);
                  Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(newCert, newBinding.CertificateHash));
                  Assert.AreEqual(bindingPort, newBinding.Port);
                  Assert.AreEqual(expectedFlags, newBinding.SSLFlags);
                  return true;
              });

            var oldips = existingBindings.Select(x => x.IP).Distinct();
            var newips = newBindings.Select(x => x.IP).Distinct();
            if (bindingIp == DefaultIP)
            {
                Assert.AreEqual(newips.Count(), oldips.Count());
                Assert.IsTrue(oldips.All(ip => newips.Contains(ip)));
            } 
            else
            {
                Assert.AreEqual(1, newips.Count());
                Assert.AreEqual(newips.First(), bindingIp);
            }
        }

        [TestMethod]
        // Basic
        [DataRow(DefaultStore, DefaultIP, DefaultPort, SSLFlags.None, SSLFlags.Sni)]
        // Alternative store
        [DataRow(AltStore, DefaultIP, DefaultPort, SSLFlags.None, SSLFlags.Sni)]
        // Alternative IP
        [DataRow(DefaultStore, AltIP, DefaultPort, SSLFlags.None, SSLFlags.Sni)]
        // Alternative port
        [DataRow(DefaultStore, DefaultIP, AltPort, SSLFlags.None, SSLFlags.Sni)]
        // Alternative flags
        [DataRow(DefaultStore, DefaultIP, DefaultPort, SSLFlags.CentralCertStore, SSLFlags.Sni | SSLFlags.CentralCertStore)]
        public void AddNewMultiple(string storeName, string bindingIp, int bindingPort, SSLFlags inputFlags, SSLFlags expectedFlags)
        {
            var originalBindings = new List<MockBinding> {
                new() {
                    IP = "*",
                    Port = 80,
                    Host = "site1.example.com",
                    Protocol = "http"
                },
                new() {
                    IP = "*",
                    Port = 80,
                    Host = "site2.example.com",
                    Protocol = "http"
                }
            };
            var site = new MockSite()
            {
                Id = httpOnlyId,
                Bindings = [.. originalBindings]
            };
            var iis = new MockIISClient(log)
            {
                MockSites = [site]
            };
            var bindingOptions = new BindingOptions().
                WithSiteId(httpOnlyId).
                WithIP(bindingIp).
                WithPort(bindingPort).
                WithStore(storeName).
                WithFlags(inputFlags).
                WithThumbprint(newCert);

            iis.UpdateHttpSite(new[] { 
                new DnsIdentifier("site1.example.com"), 
                new DnsIdentifier("site2.example.com")
            }, bindingOptions, oldCert1);
            Assert.HasCount(4, site.Bindings);
            foreach (var newBinding in site.Bindings.Except(originalBindings))
            {
                Assert.AreEqual("https", newBinding.Protocol);
                Assert.AreEqual(storeName, newBinding.CertificateStoreName);
                Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(newCert, newBinding.CertificateHash));
                Assert.AreEqual(bindingPort, newBinding.Port);
                Assert.AreEqual(bindingIp, newBinding.IP);
                Assert.AreEqual(expectedFlags, newBinding.SSLFlags);
            }
        }

        [TestMethod]
        // Basic
        [DataRow(DefaultStore, DefaultIP, DefaultPort, SSLFlags.None, SSLFlags.Sni)]
        // Alternative store
        [DataRow(AltStore, DefaultIP, DefaultPort, SSLFlags.None, SSLFlags.Sni)]
        // Alternative IP
        [DataRow(DefaultStore, AltIP, DefaultPort, SSLFlags.None, SSLFlags.Sni)]
        // Alternative port
        [DataRow(DefaultStore, DefaultIP, AltPort, SSLFlags.None, SSLFlags.Sni)]
        // Alternative flags
        [DataRow(DefaultStore, DefaultIP, DefaultPort, SSLFlags.CentralCertStore, SSLFlags.Sni | SSLFlags.CentralCertStore)]
        public void AddMultipleWildcard(string storeName, string bindingIp, int bindingPort, SSLFlags inputFlags, SSLFlags expectedFlags)
        {
            var originalBindings = new List<MockBinding> {
                new() {
                    IP = "*",
                    Port = 80,
                    Host = "*.example.com",
                    Protocol = "http"
                }
            };
            var site = new MockSite()
            {
                Id = httpOnlyId,
                Bindings = [.. originalBindings]
            };
            var iis = new MockIISClient(log)
            {
                MockSites = [site]
            };
            var bindingOptions = new BindingOptions().
                WithSiteId(httpOnlyId).
                WithIP(bindingIp).
                WithPort(bindingPort).
                WithStore(storeName).
                WithFlags(inputFlags).
                WithThumbprint(newCert);

            iis.UpdateHttpSite(new[] { 
                new DnsIdentifier("site1.example.com"), 
                new DnsIdentifier("site2.example.com") 
            }, bindingOptions, oldCert1);

            var expectedBindings = inputFlags.HasFlag(SSLFlags.CentralCertStore) ? 3 : 2;
            Assert.AreEqual(expectedBindings, site.Bindings.Count);
            foreach (var newBinding in site.Bindings.Except(originalBindings))
            {
                Assert.AreEqual("https", newBinding.Protocol);
                Assert.AreEqual(storeName, newBinding.CertificateStoreName);
                Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(newCert, newBinding.CertificateHash));
                Assert.AreEqual(bindingPort, newBinding.Port);
                Assert.AreEqual(bindingIp, newBinding.IP);
                Assert.AreEqual(expectedFlags, newBinding.SSLFlags);
            }
        }

        [TestMethod]
        // Basic
        [DataRow(DefaultStore, DefaultIP, DefaultPort, SSLFlags.None, SSLFlags.None)]
        // Alternative store
        [DataRow(AltStore, DefaultIP, DefaultPort, SSLFlags.None, SSLFlags.None)]
        // Alternative IP
        [DataRow(DefaultStore, AltIP, DefaultPort, SSLFlags.None, SSLFlags.None)]
        // Alternative port
        [DataRow(DefaultStore, DefaultIP, AltPort, SSLFlags.None, SSLFlags.None)]
        // Alternative flags
        [DataRow(DefaultStore, DefaultIP, DefaultPort, SSLFlags.CentralCertStore, SSLFlags.Sni | SSLFlags.CentralCertStore)]
        public void UpdateWildcardFuzzy(string storeName, string bindingIp, int bindingPort, SSLFlags inputFlags, SSLFlags expectedFlags)
        {
            var originalBindings = new List<MockBinding> {
                new() {
                    IP = DefaultIP,
                    Port = DefaultPort,
                    Host = "site1.example.com",
                    Protocol = "https",
                    CertificateHash = scopeCert
                }
            };
            var site = new MockSite()
            {
                Id = httpOnlyId,
                Bindings = [.. originalBindings]
            };
            var iis = new MockIISClient(log)
            {
                MockSites = [site]
            };
            var bindingOptions = new BindingOptions().
                WithSiteId(httpOnlyId).
                WithIP(bindingIp).
                WithPort(bindingPort).
                WithStore(storeName).
                WithFlags(inputFlags).
                WithThumbprint(newCert);

            iis.UpdateHttpSite(new[] { new DnsIdentifier("*.example.com") }, bindingOptions, oldCert1);

            var expectedBindings = inputFlags.HasFlag(SSLFlags.CentralCertStore) ? 2 : 1;
            Assert.AreEqual(expectedBindings, site.Bindings.Count);
            foreach (var newBinding in site.Bindings.Except(originalBindings))
            {
                Assert.AreEqual("https", newBinding.Protocol);
                Assert.AreEqual(storeName, newBinding.CertificateStoreName);
                Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(newCert, newBinding.CertificateHash));
                Assert.AreEqual(DefaultPort, newBinding.Port);
                Assert.AreEqual(DefaultIP, newBinding.IP);
                Assert.AreEqual(expectedFlags, newBinding.SSLFlags);
            }
        }

        [TestMethod]
        // Basic
        [DataRow(DefaultStore, DefaultIP, DefaultPort, SSLFlags.None, SSLFlags.Sni)]
        // Alternative store
        [DataRow(AltStore, DefaultIP, DefaultPort, SSLFlags.None, SSLFlags.Sni)]
        // Alternative IP
        [DataRow(DefaultStore, AltIP, DefaultPort, SSLFlags.None, SSLFlags.Sni)]
        // Alternative port
        [DataRow(DefaultStore, DefaultIP, AltPort, SSLFlags.None, SSLFlags.Sni)]
        // Alternative flags
        [DataRow(DefaultStore, DefaultIP, DefaultPort, SSLFlags.CentralCertStore, SSLFlags.Sni | SSLFlags.CentralCertStore)]
        public void AddMultipleWildcard2(string storeName, string bindingIp, int bindingPort, SSLFlags inputFlags, SSLFlags expectedFlags)
        {
            var originalBindings = new List<MockBinding> {
                new() {
                    IP = "*",
                    Port = 80,
                    Host = "a.example.com",
                    Protocol = "http"
                }
            };
            var site = new MockSite()
            {
                Id = httpOnlyId,
                Bindings = [.. originalBindings]
            };
            var iis = new MockIISClient(log)
            {
                MockSites = [site]
            };
            var bindingOptions = new BindingOptions().
                WithSiteId(httpOnlyId).
                WithIP(bindingIp).
                WithPort(bindingPort).
                WithStore(storeName).
                WithFlags(inputFlags).
                WithThumbprint(newCert);

            iis.UpdateHttpSite(new[] { new DnsIdentifier("*.example.com") }, bindingOptions, oldCert1);

            var expectedBindings = 2;
            Assert.AreEqual(expectedBindings, site.Bindings.Count);
            foreach (var newBinding in site.Bindings.Except(originalBindings))
            {
                Assert.AreEqual("https", newBinding.Protocol);
                Assert.AreEqual(storeName, newBinding.CertificateStoreName);
                Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(newCert, newBinding.CertificateHash));
                Assert.AreEqual(bindingPort, newBinding.Port);
                Assert.AreEqual(bindingIp, newBinding.IP);
                Assert.AreEqual(expectedFlags, newBinding.SSLFlags);
            }
        }

        [TestMethod]
        // Basic
        [DataRow(DefaultStore, DefaultIP, DefaultPort, SSLFlags.None, SSLFlags.None)]
        // Alternative store
        [DataRow(AltStore, DefaultIP, DefaultPort, SSLFlags.None, SSLFlags.None)]
        // Alternative IP
        [DataRow(DefaultStore, AltIP, DefaultPort, SSLFlags.None, SSLFlags.None)]
        // Alternative port
        [DataRow(DefaultStore, DefaultIP, AltPort, SSLFlags.None, SSLFlags.None)]
        // Alternative flags
        [DataRow(DefaultStore, DefaultIP, DefaultPort, SSLFlags.CentralCertStore, SSLFlags.CentralCertStore)]
        public void UpdateSimple(string storeName, string bindingIp, int bindingPort, SSLFlags inputFlags, SSLFlags expectedFlags)
        {
            var iis = new MockIISClient(log)
            {
                MockSites = [
                new MockSite() {
                    Id = regularId,
                    Bindings = [
                        new() {
                            IP = "*",
                            Port = 80,
                            Host = regularHost,
                            Protocol = "http"
                        },
                        new() {
                            IP = AltIP,
                            Port = AltPort,
                            Host = regularHost,
                            Protocol = "https",
                            CertificateHash = oldCert1,
                            CertificateStoreName = AltStore,
                            SSLFlags = SSLFlags.None
                        }
                    ]
                }
            ]
            };

            var bindingOptions = new BindingOptions().
                WithSiteId(regularId).
                WithIP(bindingIp).
                WithPort(bindingPort).
                WithStore(storeName).
                WithFlags(inputFlags).
                WithThumbprint(newCert);

            var regularSite = iis.GetSite(regularId);
            iis.UpdateHttpSite(new[] { new DnsIdentifier(regularHost) }, bindingOptions, oldCert1);
            Assert.HasCount(2, regularSite.Bindings);

            var updatedBinding = regularSite.Bindings[1];
            Assert.AreEqual(regularHost, updatedBinding.Host);
            Assert.AreEqual("https", updatedBinding.Protocol);
            Assert.AreEqual(storeName, updatedBinding.CertificateStoreName);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(newCert, updatedBinding.CertificateHash));
            Assert.AreEqual(AltPort, updatedBinding.Port);
            Assert.AreEqual(AltIP, updatedBinding.IP);
            Assert.AreEqual(expectedFlags, updatedBinding.SSLFlags);
        }

        [TestMethod]
        // Basic
        [DataRow(
            SSLFlags.CentralCertStore, 
            SSLFlags.CentralCertStore, 
            SSLFlags.CentralCertStore)]
        [DataRow(
            SSLFlags.Sni | SSLFlags.DisableHTTP2 | SSLFlags.DisableTLS13, 
            SSLFlags.None,
            SSLFlags.Sni | SSLFlags.DisableHTTP2 | SSLFlags.DisableTLS13)]
        // Change store
        [DataRow(
            SSLFlags.Sni | SSLFlags.DisableHTTP2 | SSLFlags.DisableTLS13,
            SSLFlags.CentralCertStore,
            SSLFlags.Sni | SSLFlags.CentralCertStore)]
        [DataRow(
            SSLFlags.DisableHTTP2 | SSLFlags.DisableTLS13,
            SSLFlags.CentralCertStore,
            SSLFlags.CentralCertStore)]
        // Set SNI
        [DataRow(
            SSLFlags.DisableHTTP2 | SSLFlags.DisableTLS13,
            SSLFlags.Sni,
            SSLFlags.Sni | SSLFlags.DisableHTTP2 | SSLFlags.DisableTLS13)]
        [DataRow(
            SSLFlags.CentralCertStore,
            SSLFlags.Sni | SSLFlags.CentralCertStore,
            SSLFlags.Sni | SSLFlags.CentralCertStore)]
         public void PreserveFlags(SSLFlags initialFlags, SSLFlags inputFlags, SSLFlags expectedFlags)
        {
            var iis = new MockIISClient(log)
            {
                MockSites = [
                new MockSite() {
                    Id = regularId,
                    Bindings = [
                        new() {
                            IP = AltIP,
                            Port = AltPort,
                            Host = "host.nl",
                            Protocol = "https",
                            CertificateHash = oldCert1,
                            CertificateStoreName = AltStore,
                            SSLFlags = initialFlags
                        }
                    ]
                }
            ]
            };

            var bindingOptions = new BindingOptions().
                WithSiteId(regularId).
                WithFlags(inputFlags).
                WithThumbprint(newCert);

            var regularSite = iis.GetSite(regularId);
            iis.UpdateHttpSite(new[] { new DnsIdentifier("host.nl") }, bindingOptions, oldCert1);
            Assert.HasCount(1, regularSite.Bindings);

            var updatedBinding = regularSite.Bindings.FirstOrDefault();
            Assert.IsNotNull(updatedBinding);
            Assert.AreEqual("https", updatedBinding?.Protocol);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(newCert, updatedBinding?.CertificateHash));
            Assert.AreEqual(expectedFlags, updatedBinding?.SSLFlags);
        }

        [TestMethod]
        public void UpdateOutOfScopeCatchAll()
        {
            var iis = new MockIISClient(log)
            {
                MockSites = [
                    new MockSite() {
                        Id = inscopeId,
                        Bindings = [
                            new() {
                                IP = DefaultIP,
                                Port = DefaultPort,
                                Host = inscopeHost,
                                Protocol = "https",
                                CertificateHash = scopeCert,
                                CertificateStoreName = DefaultStore,
                                SSLFlags = SSLFlags.Sni
                            }
                        ]
                    },
                    new MockSite() {
                        Id = outofscopeId,
                        Bindings = [
                            new() {
                                IP = DefaultIP,
                                Port = DefaultPort,
                                Host = "",
                                Protocol = "https",
                                CertificateHash = scopeCert,
                                CertificateStoreName = DefaultStore,
                                SSLFlags = SSLFlags.Sni
                            }
                        ]
                    }
                ]
            };

            var bindingOptions = new BindingOptions().
                WithSiteId(inscopeId).
                WithIP(DefaultIP).
                WithPort(DefaultPort).
                WithStore(DefaultStore).
                WithThumbprint(newCert);

            var outofScopeSite = iis.GetSite(outofscopeId);
            iis.UpdateHttpSite(new[] { new DnsIdentifier(regularHost) }, bindingOptions, scopeCert);
            Assert.HasCount(1, outofScopeSite.Bindings);

            var updatedBinding = outofScopeSite.Bindings[0];
            Assert.AreEqual(DefaultStore, updatedBinding.CertificateStoreName);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(newCert, updatedBinding.CertificateHash));
        }

        [TestMethod]
        public void UpdateOutOfScopeRegular()
        {
            var iis = new MockIISClient(log)
            {
                MockSites = [
                    new MockSite() {
                        Id = inscopeId,
                        Bindings = [
                            new() {
                                IP = DefaultIP,
                                Port = DefaultPort,
                                Host = inscopeHost,
                                Protocol = "https",
                                CertificateHash = scopeCert,
                                CertificateStoreName = DefaultStore,
                                SSLFlags = SSLFlags.Sni
                            }
                        ]
                    },
                    new MockSite() {
                        Id = outofscopeId,
                        Bindings = [
                            new() {
                                IP = DefaultIP,
                                Port = DefaultPort,
                                Host = outofscopeHost,
                                Protocol = "https",
                                CertificateHash = scopeCert,
                                CertificateStoreName = DefaultStore,
                                SSLFlags = SSLFlags.Sni
                            }
                        ]
                    }
                ]
            };

            var bindingOptions = new BindingOptions().
                WithSiteId(inscopeId).
                WithIP(DefaultIP).
                WithPort(DefaultPort).
                WithStore(DefaultStore).
                WithThumbprint(newCert);

            var outofScopeSite = iis.GetSite(outofscopeId);
            iis.UpdateHttpSite(new[] { new DnsIdentifier(regularHost) }, bindingOptions, scopeCert);
            Assert.HasCount(1, outofScopeSite.Bindings);

            var updatedBinding = outofScopeSite.Bindings[0];
            Assert.AreEqual(DefaultStore, updatedBinding.CertificateStoreName);
            Assert.AreEqual(scopeCert, updatedBinding.CertificateHash);
        }

        [TestMethod]
        [DataRow("a.b.c.com", new string[] { }, "a.b.c.com", SSLFlags.None)]
        [DataRow("a.b.c.com", new[] { "a.b.c.com" }, "*.b.c.com", SSLFlags.None)]
        [DataRow("a.b.c.com", new[] { "a.b.c.com", "*.b.c.com" }, "*.c.com", SSLFlags.None)]
        [DataRow("a.b.c.com", new[] { "a.b.c.com", "*.b.c.com", "*.c.com" }, "*.com", SSLFlags.None)]
        [DataRow("a.b.c.com", new[] { "a.b.c.com", "*.b.c.com", "*.c.com", "*.com" }, "", SSLFlags.None)]
        [DataRow("a.b.c.com", new[] { "a.b.c.com", "*.b.c.com", "*.c.com", "*.com", "" }, "a.b.c.com", SSLFlags.None)]

        [DataRow("*.b.c.com", new string[] { }, "*.b.c.com", SSLFlags.None)]
        [DataRow("*.b.c.com", new[] { "*.b.c.com" }, "a.b.c.com", SSLFlags.None)]
        [DataRow("*.b.c.com", new[] { "a.b.c.com", "*.b.c.com", "*.c.com", "*.com", "" }, "*.b.c.com", SSLFlags.None)]

        [DataRow("a.b.c.com", new[] { "a.b.c.com" }, "a.b.c.com", SSLFlags.CentralCertStore)]
        [DataRow("a.b.c.com", new[] { "a.b.c.com", "*.b.c.com" }, "a.b.c.com", SSLFlags.CentralCertStore)]

        [DataRow("*.b.c.com", new string[] { }, "*.b.c.com", SSLFlags.CentralCertStore)]
        [DataRow("*.b.c.com", new[] { "*.b.c.com" }, "*.b.c.com", SSLFlags.CentralCertStore)]
        [DataRow("*.b.c.com", new[] { "a.b.c.com", "*.b.c.com", "*.c.com", "*.com" }, "*.b.c.com", SSLFlags.CentralCertStore)]
        [DataRow("*.b.c.com", new[] { "a.b.c.com", "*.b.c.com", "*.c.com", "*.com", "" }, "*.b.c.com", SSLFlags.CentralCertStore)]
        public void UpdatePiramid(string certificateHost, string[] ignoreBindings, string expectedBinding, SSLFlags flags)
        {
            var iis = new MockIISClient(log)
            {
                MockSites = [
                    new MockSite() {
                    Id = piramidId,
                    Bindings = [
                        new() {
                            IP = DefaultIP,
                            Port = 80,
                            Host = "a.b.c.com",
                            Protocol = "http"
                        },
                        new() {
                            IP = DefaultIP,
                            Port = 80,
                            Host = "*.b.c.com",
                            Protocol = "http"
                        },
                        new() {
                            IP = DefaultIP,
                            Port = 80,
                            Host = "*.x.y.z.com",
                            Protocol = "http"
                        },
                        new() {
                            IP = DefaultIP,
                            Port = 80,
                            Host = "*.c.com",
                            Protocol = "http"
                        },
                        new() {
                            IP = DefaultIP,
                            Port = 80,
                            Host = "*.com",
                            Protocol = "http"
                        },
                        new() {
                            IP = DefaultIP,
                            Port = 80,
                            Host = "",
                            Protocol = "http"
                        }
                    ]
                }
            ]
            };

            var bindingOptions = new BindingOptions().
                WithSiteId(piramidId).
                WithIP(DefaultIP).
                WithPort(DefaultPort).
                WithStore(DefaultStore).
                WithThumbprint(newCert).
                WithFlags(flags);

            var piramidSite = iis.GetSite(piramidId);
            var originalSet = piramidSite.Bindings.Where(x => !ignoreBindings.Contains(x.Host)).ToList();
            piramidSite.Bindings = [.. originalSet.ToList().OrderBy(x => Guid.NewGuid())];
            iis.UpdateHttpSite(new[] { new DnsIdentifier(certificateHost) }, bindingOptions, scopeCert);

            var newBindings = piramidSite.Bindings.Except(originalSet);
            Assert.AreEqual(1, newBindings.Count());

            var newBinding = newBindings.First();
            Assert.AreEqual(expectedBinding, newBinding.Host);
        }

        [TestMethod]
        [DataRow(1, 7)]
        [DataRow(1, 8)]
        [DataRow(2, 10)]
        public void WildcardOld(int expectedBindings, int iisVersion)
        {
            var site = new MockSite()
            {
                Id = piramidId,
                Bindings = [
                        new() {
                            IP = DefaultIP,
                            Port = 80,
                            Host = "*.b.c.com",
                            Protocol = "http"
                        }
                    ]
            };
            var iis = new MockIISClient(log, iisVersion)
            {
                MockSites = [site]
            };

            var bindingOptions = new BindingOptions().
                WithSiteId(piramidId).
                WithIP(DefaultIP).
                WithPort(DefaultPort).
                WithStore(DefaultStore).
                WithThumbprint(newCert);

            iis.UpdateHttpSite(new[] { new DnsIdentifier("*.b.c.com") }, bindingOptions, scopeCert);

            Assert.AreEqual(expectedBindings, site.Bindings.Count);
        }

        /// <summary>
        /// SNI should be turned on when an existing HTTPS binding
        /// without SNI is modified but another HTTPS binding, 
        /// also without SNI, is listening on the same IP and port.
        /// </summary>
        [TestMethod]
        public void SNITrap1()
        {
            var iis = new MockIISClient(log)
            {
                MockSites = [
                    new MockSite() {
                        Id = sniTrap1,
                        Bindings = [
                            new() {
                                IP = DefaultIP,
                                Port = DefaultPort,
                                Host = sniTrapHost,
                                Protocol = "https",
                                CertificateHash = oldCert1,
                                CertificateStoreName = DefaultStore,
                                SSLFlags = SSLFlags.None
                            }
                        ]
                    },
                    new MockSite() {
                        Id = sniTrap2,
                        Bindings = [
                            new() {
                                IP = DefaultIP,
                                Port = DefaultPort,
                                Host = "",
                                Protocol = "https",
                                CertificateHash = oldCert1,
                                CertificateStoreName = DefaultStore,
                                SSLFlags = SSLFlags.None
                            }
                        ]
                    },
                ]
            };

            var bindingOptions = new BindingOptions().
                WithSiteId(sniTrap1).
                WithIP(DefaultIP).
                WithPort(DefaultPort).
                WithStore(DefaultStore).
                WithThumbprint(newCert);

            var sniTrap1Site = iis.GetSite(sniTrap1);
            iis.UpdateHttpSite(new[] { new DnsIdentifier(sniTrapHost) }, bindingOptions, scopeCert);

            var updatedBinding = sniTrap1Site.Bindings[0];
            Assert.AreEqual(SSLFlags.Sni, updatedBinding.SSLFlags);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(newCert, updatedBinding.CertificateHash));
        }

        /// <summary>
        /// Like above, but SNI cannot be turned on for the default
        /// website / empty host. The code should ignore the change.
        /// </summary>
        [TestMethod]
        public void SNITrap2()
        {
            var iis = new MockIISClient(log)
            {
                MockSites = [
                    new MockSite() {
                        Id = sniTrap1,
                        Bindings = [
                            new() {
                                IP = DefaultIP,
                                Port = DefaultPort,
                                Host = sniTrapHost,
                                Protocol = "https",
                                CertificateHash = oldCert1,
                                CertificateStoreName = DefaultStore,
                                SSLFlags = SSLFlags.None
                            }
                        ]
                    },
                    new MockSite() {
                        Id = sniTrap2,
                        Bindings = [
                            new() {
                                IP = DefaultIP,
                                Port = DefaultPort,
                                Host = "",
                                Protocol = "https",
                                CertificateHash = oldCert1,
                                CertificateStoreName = DefaultStore,
                                SSLFlags = SSLFlags.None
                            }
                        ]
                    },
                ]
            };

            var bindingOptions = new BindingOptions().
                WithSiteId(sniTrap2).
                WithIP(DefaultIP).
                WithPort(DefaultPort).
                WithStore(DefaultStore).
                WithThumbprint(newCert);

            var sniTrap2Site = iis.GetSite(sniTrap2);
            iis.UpdateHttpSite(new[] { new DnsIdentifier(sniTrapHost) }, bindingOptions, scopeCert);

            var untouchedBinding = sniTrap2Site.Bindings[0];
            Assert.AreEqual(SSLFlags.None, untouchedBinding.SSLFlags);
            Assert.AreEqual(oldCert1, untouchedBinding.CertificateHash);
            Assert.HasCount(1, sniTrap2Site.Bindings);
        }

        /// <summary>
        /// Like above, but the new domain is different so a separate binding should
        /// be created for it
        /// </summary>
        [TestMethod]
        public void SNITrap3()
        {
            var iis = new MockIISClient(log)
            {
                MockSites = [
                    new() {
                        Id = sniTrap1,
                        Bindings = [
                            new() {
                                IP = DefaultIP,
                                Port = DefaultPort,
                                Host = sniTrapHost,
                                Protocol = "https",
                                CertificateHash = oldCert1,
                                CertificateStoreName = DefaultStore,
                                SSLFlags = SSLFlags.None
                            }
                        ]
                    },
                    new() {
                        Id = sniTrap2,
                        Bindings = [
                            new() {
                                IP = DefaultIP,
                                Port = DefaultPort,
                                Host = "",
                                Protocol = "https",
                                CertificateHash = oldCert1,
                                CertificateStoreName = DefaultStore,
                                SSLFlags = SSLFlags.None
                            }
                        ]
                    },
                ]
            };

            var bindingOptions = new BindingOptions().
                WithSiteId(sniTrap2).
                WithIP(DefaultIP).
                WithPort(DefaultPort).
                WithStore(DefaultStore).
                WithThumbprint(newCert);

            var sniTrap2Site = iis.GetSite(sniTrap2);
            iis.UpdateHttpSite(new[] { new DnsIdentifier("example.com") }, bindingOptions, scopeCert);

            var untouchedBinding = sniTrap2Site.Bindings[0];
            Assert.AreEqual(SSLFlags.None, untouchedBinding.SSLFlags);
            Assert.AreEqual(oldCert1, untouchedBinding.CertificateHash);
            Assert.HasCount(2, sniTrap2Site.Bindings);
            var newBinding = sniTrap2Site.Bindings[1];
            Assert.AreEqual(SSLFlags.Sni, newBinding.SSLFlags);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(newCert, newBinding.CertificateHash));
        }


        /// <summary>
        /// Like above, but SNI cannot be turned on for the default
        /// website / empty host. The code should ignore the change.
        /// </summary>
        [TestMethod]
        public void CentralSSLTrap()
        {
            var iis = new MockIISClient(log)
            {
                MockSites = [
                    new() {
                        Id = 1,
                        Bindings = [
                            new() {
                                IP = DefaultIP,
                                Port = DefaultPort,
                                Host = "",
                                Protocol = "http"
                            }
                        ]
                    }
                ]
            };

            var bindingOptions = new BindingOptions().
                WithSiteId(1).
                WithIP(DefaultIP).
                WithPort(DefaultPort).
                WithFlags(SSLFlags.CentralCertStore);

            iis.UpdateHttpSite(new[] { new DnsIdentifier("mail.example.com") }, bindingOptions, null);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void DuplicateBinding()
        {
            var dup1 = new MockSite()
            {
                Id = 1,
                Bindings = [
                            new() {
                                IP = DefaultIP,
                                Port = DefaultPort,
                                Host = "exists.example.com",
                                Protocol = "https",
                                CertificateHash = oldCert1,
                                CertificateStoreName = DefaultStore,
                                SSLFlags = SSLFlags.None
                            }
                        ]
            };

            var dup2 = new MockSite()
            {
                Id = 2,
                Bindings = [
                    new() {
                        IP = DefaultIP,
                        Port = 80,
                        Host = "exists.example.com",
                        Protocol = "http"
                    }
                ]
            };

            var iis = new MockIISClient(log)
            {
                MockSites = [dup1, dup2]
            };

            var bindingOptions = new BindingOptions().
                WithSiteId(2).
                WithIP(DefaultIP).
                WithPort(DefaultPort).
                WithStore(DefaultStore).
                WithThumbprint(newCert);

            iis.UpdateHttpSite(new[] { new DnsIdentifier("exists.example.com") }, bindingOptions, scopeCert);
            Assert.HasCount(1, dup2.Bindings);
        }

        [TestMethod]
        [DataRow(7, 1)]
        [DataRow(8, 2)]
        [DataRow(10, 2)]
        public void DuplicateHostBindingW2K8(int iisVersion, int expectedBindings)
        {
            var dup1 = new MockSite()
            {
                Id = 1,
                Bindings = [
                            new() {
                                IP = DefaultIP,
                                Port = DefaultPort,
                                Host = "exists.example.com",
                                Protocol = "https",
                                CertificateHash = oldCert1,
                                CertificateStoreName = DefaultStore
                            }
                        ]
            };

            var dup2 = new MockSite()
            {
                Id = 2,
                Bindings = [
                    new() {
                        IP = DefaultIP,
                        Port = 80,
                        Host = "new.example.com",
                        Protocol = "http"
                    }
                ]
            };

            var iis = new MockIISClient(log, iisVersion)
            {
                MockSites = [dup1, dup2]
            };

            var bindingOptions = new BindingOptions().
                WithSiteId(2).
                WithIP(DefaultIP).
                WithPort(DefaultPort).
                WithStore(DefaultStore).
                WithThumbprint(newCert);

            iis.UpdateHttpSite(new[] { new DnsIdentifier("new.example.com") }, bindingOptions, scopeCert);
            Assert.AreEqual(expectedBindings, dup2.Bindings.Count);
        }
        [TestMethod]
        public void MultipleDontCreate()
        {
            var dup1 = new MockSite()
            {
                Id = 1,
                Bindings = [
                            new() {
                                IP = "*",
                                Port = 8080,
                                Host = "wacs1.test.net",
                                Protocol = "https",
                                SSLFlags = SSLFlags.None,
                                CertificateHash = oldCert1,
                                CertificateStoreName = DefaultStore
                            }
                        ]
            };

            var dup2 = new MockSite()
            {
                Id = 2,
                Bindings = [
                    new() {
                        IP = "*",
                        Port = 444,
                        Host = "wacs1.test.net",
                        Protocol = "https",
                        SSLFlags = SSLFlags.Sni,
                        CertificateHash = oldCert1,
                        CertificateStoreName = DefaultStore
                    }
                ]
            };

            var iis = new MockIISClient(log, 10)
            {
                MockSites = [dup1, dup2]
            };

            var bindingOptions = new BindingOptions().
                WithFlags(SSLFlags.None).
                WithStore(DefaultStore).
                WithThumbprint(newCert);

            log.Information("Site 1");
            iis.UpdateHttpSite(new[] {
                new DnsIdentifier("wacs1.test.net")
            },
            bindingOptions.WithSiteId(1),
            oldCert1);

            log.Information("Site 2");

            iis.UpdateHttpSite(new[] {
                new DnsIdentifier("wacs1.test.net")
            },
            bindingOptions.WithSiteId(2),
            oldCert1);

            Assert.HasCount(1, dup2.Bindings);
        }

        [TestMethod]
        public void MultipleNonSNI()
        {
            var dup1 = new MockSite()
            {
                Id = 1,
                Bindings = [
                            new() {
                                IP = "*",
                                Port = 80,
                                Host = "wacs1.test.net",
                                Protocol = "http"
                            },
                            new() {
                                IP = "*",
                                Port = 443,
                                Host = "wacs1.test.net",
                                Protocol = "https",
                                SSLFlags = SSLFlags.None,
                                CertificateHash = oldCert1,
                                CertificateStoreName = DefaultStore
                            }
                        ]
            };

            var dup2 = new MockSite()
            {
                Id = 2,
                Bindings = [
                    new() {
                        IP = "*",
                        Port = 80,
                        Host = "wacs2.test.net",
                        Protocol = "http"
                    },
                    new() {
                        IP = "*",
                        Port = 443,
                        Host = "wacs2.test.net",
                        Protocol = "https",
                        SSLFlags = SSLFlags.None,
                        CertificateHash = oldCert1,
                        CertificateStoreName = DefaultStore
                    },
                    new() {
                        IP = "*",
                        Port = 443,
                        Host = "wacs2alt.test.net",
                        Protocol = "https",
                        SSLFlags = SSLFlags.None,
                        CertificateHash = oldCert1,
                        CertificateStoreName = DefaultStore
                    }
                ]
            };

            var iis = new MockIISClient(log, 10)
            {
                MockSites = [dup1, dup2]
            };

            var bindingOptions = new BindingOptions().
                WithFlags(SSLFlags.None).
                WithStore(DefaultStore).
                WithThumbprint(newCert);

            iis.UpdateHttpSite(new[] { 
                new DnsIdentifier("wacs1.test.net"), 
                new DnsIdentifier("wacs2.test.net"), 
                new DnsIdentifier("wacs2alt.test.net") 
            },
            bindingOptions.WithSiteId(1),
            oldCert1);

            log.Information("Site 2");

            iis.UpdateHttpSite(new[] {
                new DnsIdentifier("wacs1.test.net"),
                new DnsIdentifier("wacs2.test.net"),
                new DnsIdentifier("wacs2alt.test.net")
            },
            bindingOptions.WithSiteId(2),
            oldCert1);
        }

        [DataRow(7, "")]
        [DataRow(10, "")]
        [DataRow(7, "exists.example.com")]
        [DataRow(10, "exists.example.com")]
        [TestMethod]
        public void IPv4andIPv6(int iisVersion, string host)
        {
            var dup1 = new MockSite()
            {
                Id = 1,
                Bindings = [
                            new() {
                                IP = DefaultIP,
                                Port = DefaultPort,
                                Host = host,
                                Protocol = "https",
                                CertificateHash = oldCert1,
                                CertificateStoreName = DefaultStore
                            },
                            new() {
                                IP = "FE80:CD00:0000:0CDE:1257:0000:211E:729C",
                                Port = DefaultPort,
                                Host = host,
                                Protocol = "https",
                                CertificateHash = oldCert1,
                                CertificateStoreName = DefaultStore
                            }
                        ]
            };

            var iis = new MockIISClient(log, iisVersion)
            {
                MockSites = [dup1]
            };

            var bindingOptions = new BindingOptions().
                WithSiteId(1).
                WithIP(DefaultIP).
                WithPort(DefaultPort).
                WithStore(DefaultStore).
                WithThumbprint(newCert);

            iis.UpdateHttpSite(new[] { new DnsIdentifier("exists.example.com") }, bindingOptions, oldCert1);
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(newCert, iis.Sites.First().Bindings.First().CertificateHash));
            Assert.IsTrue(StructuralComparisons.StructuralEqualityComparer.Equals(newCert, iis.Sites.First().Bindings.Last().CertificateHash));
        }

        [TestMethod]
        [DataRow("UPPERCASE.example.com", "UPPERCASE.example.com", "UPPERCASE.example.com")]
        [DataRow("uppercase.example.com", "UPPERCASE.example.com", "UPPERCASE.example.com")]
        [DataRow("UPPERCASE.example.com", "uppercase.example.com", "UPPERCASE.example.com")]
        [DataRow("UPPERCASE.example.com", "UPPERCASE.example.com", "uppercase.example.com")]
        [DataRow("UPPERCASE.example.com", "uppercase.example.com", "uppercase.example.com")]
        [DataRow("uppercase.example.com", "UPPERCASE.example.com", "uppercase.example.com")]
        [DataRow("uppercase.example.com", "uppercase.example.com", "UPPERCASE.example.com")]
        [DataRow("uppercase.example.com", "uppercase.example.com", "uppercase.example.com")]
        public void UppercaseBinding(string host, string bindingInfo, string newHost)
        {
            var mockBinding = new MockBinding
            {
                IP = "*",
                Port = 443,
                Host = host,
                Protocol = "https",
                CertificateHash = oldCert1,
                CertificateStoreName = DefaultStore,
                BindingInformation = $"*:443:{bindingInfo}"
            };

            var dup1 = new MockSite()
            {
                Id = 1,
                Bindings = [ 
                    mockBinding, 
                    new()
                    {
                        IP = "*",
                        Port = 80,
                        Host = host,
                        Protocol = "http"
                    }
                ]
            };

            var iis = new MockIISClient(log, 10)
            {
                MockSites = [dup1]
            };

            var bindingOptions = new BindingOptions().
                WithSiteId(1).
                WithStore(DefaultStore).
                WithThumbprint(newCert);

            iis.UpdateHttpSite(new[] { new DnsIdentifier(host), new DnsIdentifier(newHost) }, bindingOptions, null);
            Assert.HasCount(2, iis.Sites.First().Bindings);
        }
    }
}