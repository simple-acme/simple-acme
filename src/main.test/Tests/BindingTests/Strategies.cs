using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.UnitTests.Mock.Clients;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace PKISharp.WACS.UnitTests.Tests.BindingTests
{
    [TestClass]
    [SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments", Justification = "Not supported by decorators")]
    public class BindingStrategyTests
    {
        private readonly Mock.Services.LogService log;
        private readonly byte[] newCert = [0x2];
        private readonly byte[] oldCert1 = [0x1];
        public BindingStrategyTests() => log = new Mock.Services.LogService(false);

        private MockIISClient IIS => new(log, 10)
        {
            MockSites = [
                new MockSite() {
                    Id = 1,
                    Bindings = [
                        new() {
                            Id = 1,
                            IP = "*",
                            Port = 443,
                            Host = "wouter.tinus.online",
                            CertificateHash = oldCert1,
                            Protocol = "https"
                        },
                        new() {
                            Id = 4,
                            IP = "*",
                            Port = 443,
                            Host = "other.tinus.online",
                            CertificateHash = oldCert1,
                            Protocol = "https"
                        },
                        new() {
                            Id = 2,
                            IP = "*",
                            Port = 4443,
                            Host = "wouter.tinus.online",
                            Protocol = "https"
                        }
                    ]
                },
                new MockSite() {
                    Id = 2,
                    Bindings = [
                        new() {
                            Id = 3,
                            IP = "*",
                            Port = 44503,
                            Host = "wouter.tinus.online",
                            Protocol = "https"
                        },
                        new() {
                            Id = 5,
                            IP = "*",
                            Port = 443,
                            Host = "sub.wouter.tinus.online",
                            CertificateHash = oldCert1,
                            Protocol = "https"
                        },
                        new() {
                            Id = 6,
                            IP = "*",
                            Port = 443,
                            Host = "*.tinus.online",
                            Protocol = "https"
                        }
                    ]
                }
            ]
        };

        [TestMethod]
        [DataRow(
            ReplaceMode.Thumbprint, 
            new[] { "wouter.tinus.online" }, 
            new[] { 1 }, 
            null,
            DisplayName = "Thumbprint - basic")]
        [DataRow(
            ReplaceMode.Thumbprint, 
            new[] { "wouter.tinus.online" }, 
            new[] { 1 }, 
            (long)2, 
            DisplayName = "Thumbprint - other site")]
        [DataRow(ReplaceMode.Thumbprint | ReplaceMode.ExactMatch, 
            new[] { "wouter.tinus.online" }, 
            new[] { 1, 2 }, 
            (long)1,
            DisplayName = "ExactMatch - specific site exact")]
        [DataRow(
            ReplaceMode.Thumbprint | ReplaceMode.ExactMatch, 
            new[] { "wouter.tinus.online" },
            new[] { 1, 2, 3 }, 
            null, 
            DisplayName = "ExactMatch - all sites")]
        [DataRow(
            ReplaceMode.Thumbprint | ReplaceMode.ExactMatch,
            new[] { "*.tinus.online" },
            new[] { 1, 4, 6 },
            null,
            DisplayName = "ExactMatchWildcard - all sites")]
        [DataRow(
            ReplaceMode.Thumbprint | ReplaceMode.ExactMatch | ReplaceMode.WildcardMatch, 
            new[] { "wouter.tinus.online" }, 
            new[] { 1, 2 }, 
            (long)1, 
            DisplayName = "Unneeded wildcardMatch - specific site")]
        [DataRow(
            ReplaceMode.Thumbprint | ReplaceMode.ExactMatch | ReplaceMode.WildcardMatch, 
            new[] { "wouter.tinus.online" }, 
            new[] { 1, 2, 3 }, 
            null,
            DisplayName = "Unneeded wildcardMatch - all sites")]
        [DataRow(
            ReplaceMode.Thumbprint | ReplaceMode.ExactMatch | ReplaceMode.WildcardMatch,
            new[] { "*.tinus.online" },
            new[] { 1, 2, 4 },
            (long)1,
            DisplayName = "WildcardMatch - all sites")]
        [DataRow(
            ReplaceMode.Thumbprint | ReplaceMode.ExactMatch | ReplaceMode.WildcardMatch,
            new[] { "*.tinus.online" },
            new[] { 1, 2, 3, 4, 6 },
            null,
            DisplayName = "WildcardMatch - specific site")]
        public void RegularHost(ReplaceMode mode, string[] identifiers, int[] matchesExpected, long? siteId)
        {
            var iis = IIS;
            var bindingOptions = new BindingOptions();
            bindingOptions = bindingOptions.WithThumbprint(newCert).WithSiteId(siteId);
            
            iis.UpdateHttpSite(identifiers.Select(i => new DnsIdentifier(i)), bindingOptions, oldCert1, replaceMode: mode);
            
            var allBindings = iis.Sites.SelectMany(x => x.Bindings).ToList();
            var shouldBeUpdated = allBindings.Where(b => matchesExpected.Contains(b.Id)).ToList();
            CollectionAssert.AreEquivalent(matchesExpected, shouldBeUpdated.Select(x => x.Id).ToList(), "Not all expected bindings found in mock IIS");
    
            var shouldNotBeUpdated = allBindings.Except(shouldBeUpdated).ToList();
            var updated = allBindings.Where(x => StructuralComparisons.StructuralEqualityComparer.Equals(x.CertificateHash, newCert));
            var notUpdated = allBindings.Where(x => !StructuralComparisons.StructuralEqualityComparer.Equals(x.CertificateHash, newCert));

            var updatedFormat = string.Join(",", updated.Select(x => x.Id).Order());
            var notUpdatedFormat = string.Join(",", notUpdated.Select(x => x.Id).Order());
            var shouldBeUpdatedFormat = string.Join(",", shouldBeUpdated.Select(x => x.Id).Order());
            var shouldNotBeUpdatedFormat = string.Join(",", shouldNotBeUpdated.Select(x => x.Id).Order());
           
            Assert.AreEqual(shouldBeUpdatedFormat, updatedFormat, "Updated bindings do not match expected");
            Assert.AreEqual(shouldNotBeUpdatedFormat, notUpdatedFormat, "Not updated bindings do not match expected");
        }
    }
}