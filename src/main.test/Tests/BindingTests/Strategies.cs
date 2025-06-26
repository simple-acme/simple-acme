using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.UnitTests.Mock.Clients;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static Org.BouncyCastle.Crypto.Engines.SM2Engine;

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
                        },
                        new() {
                            Id = 7,
                            IP = "*",
                            Port = 443,
                            Host = "*.wildcard.example.com",
                            Protocol = "https"
                        },
                        new() {
                            Id = 8,
                            IP = "*",
                            Port = 443,
                            Host = "sub.wildcard.example.com",
                            Protocol = "https"
                        },
                        new() {
                            Id = 11,
                            IP = "*",
                            Port = 443,
                            Host = "duplicate.tinus.com",
                            Protocol = "https"
                        }
                    ]
                },
                new MockSite() {
                    Id = 3,
                    Bindings = [
                        new() {
                            Id = 9,
                            IP = "*",
                            Port = 80,
                            Host = "adder.tinus.online",
                            Protocol = "http"
                        },
                        new() {
                            Id = 10,
                            IP = "*",
                            Port = 80,
                            Host = "duplicate.tinus.online",
                            Protocol = "http"
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
            new string[] { },
            DisplayName = "Thumbprint - basic")]
        [DataRow(
            ReplaceMode.Thumbprint, 
            new[] { "wouter.tinus.online" }, 
            new[] { 1 }, 
            (long)2,
            new string[] { },
            DisplayName = "Thumbprint - other site")]
        [DataRow(ReplaceMode.Thumbprint | ReplaceMode.ExactMatch, 
            new[] { "wouter.tinus.online" }, 
            new[] { 1, 2 }, 
            (long)1,
            new string[] { },
            DisplayName = "ExactMatch - specific site exact")]
        [DataRow(
            ReplaceMode.Thumbprint | ReplaceMode.ExactMatch, 
            new[] { "wouter.tinus.online" },
            new[] { 1, 2, 3 }, 
            null,
            new string[] { },
            DisplayName = "ExactMatch - all sites")]
        [DataRow(
            ReplaceMode.Thumbprint | ReplaceMode.ExactMatch | ReplaceMode.WildcardMatch, 
            new[] { "wouter.tinus.online" }, 
            new[] { 1, 2 }, 
            (long)1,
            new string[] { },
            DisplayName = "Unneeded wildcardMatch - specific site")]
        [DataRow(
            ReplaceMode.Thumbprint | ReplaceMode.ExactMatch | ReplaceMode.WildcardMatch, 
            new[] { "wouter.tinus.online" }, 
            new[] { 1, 2, 3 }, 
            null,
            new string[] { },
            DisplayName = "Unneeded wildcardMatch - all sites")]
        public void RegularHost(ReplaceMode mode, string[] identifiers, int[] matchesExpected, long? siteId, string[] newBindingsExpected)
        {
            RunTest(mode, AddMode.Default, identifiers, matchesExpected, siteId, newBindingsExpected);
        }

        [DataRow(
           ReplaceMode.Thumbprint | ReplaceMode.ExactMatch,
           new[] { "*.tinus.online" },
           new[] { 1,4,6 },
           null,
           new string[] { },
           DisplayName = "ExactMatch - with old cert")]
        [DataRow(
           ReplaceMode.Thumbprint | ReplaceMode.ExactMatch,
           new[] { "*.example.com" },
           new int[] { },
           null,
           new string[] { },
           DisplayName = "No match")]
        [DataRow(
           ReplaceMode.Thumbprint | ReplaceMode.ExactMatch,
           new[] { "*.wildcard.example.com" },
           new int[] { 7 },
           null,
           new string[] { },
           DisplayName = "Single match")]
        [DataRow(
            ReplaceMode.Thumbprint | ReplaceMode.ExactMatch | ReplaceMode.WildcardMatch,
            new[] { "*.tinus.online" },
            new[] { 1, 2, 4 },
            (long)1,
            new string[] { },
            DisplayName = "WildcardMatch - all sites")]
        [DataRow(
            ReplaceMode.Thumbprint | ReplaceMode.ExactMatch | ReplaceMode.WildcardMatch,
            new[] { "*.tinus.online" },
            new[] { 1, 2, 3, 4, 6 },
            null,
            new string[] { },
            DisplayName = "WildcardMatch - specific site")]
        [TestMethod]
        public void Wildcard(ReplaceMode mode, string[] identifiers, int[] matchesExpected, long? siteId, string[] newBindingsExpected)
        {
            RunTest(mode, AddMode.Default, identifiers, matchesExpected, siteId, newBindingsExpected);
        }

        [DataRow(
             ReplaceMode.Default,
             AddMode.Default,
             new[] { "adder.tinus.online" },
             new int[] { },
             (long)3,
             new string[] { "adder.tinus.online" },
             DisplayName = "Simple add")]
        [DataRow(
            ReplaceMode.Default,
            AddMode.Default,
            new[] { "duplicate.tinus.com" },
            new int[] { },
            (long)3,
            new string[] { },
         DisplayName = "Duplicate add")]
        [TestMethod]
        public void Add(ReplaceMode replaceMode, AddMode addMode, string[] identifiers, int[] matchesExpected, long? siteId, string[] newBindingsExpected)
        {
            RunTest(replaceMode, addMode, identifiers, matchesExpected, siteId, newBindingsExpected);
        }

        public void RunTest(ReplaceMode replaceMode, AddMode addMode, string[] identifiers, int[] matchesExpected, long? siteId, string[] newBindingsExpected)
        {
            var iis = IIS;
   
            var allBindings = iis.Sites.SelectMany(x => x.Bindings).ToList();
            var shouldBeUpdated = allBindings.Where(b => matchesExpected.Contains(b.Id)).ToList();
            var shouldNotBeUpdated = allBindings.Except(shouldBeUpdated).ToList();
            CollectionAssert.AreEquivalent(matchesExpected, shouldBeUpdated.Select(x => x.Id).ToList(), "Not all expected bindings found in mock IIS");

            var bindingOptions = new BindingOptions();
            bindingOptions = bindingOptions.WithThumbprint(newCert).WithSiteId(siteId);
            var ctx = iis.UpdateHttpSite(identifiers.Select(i => new DnsIdentifier(i)), bindingOptions, oldCert1, replaceMode: replaceMode, addMode: addMode);

            allBindings = iis.Sites.SelectMany(x => x.Bindings).ToList();
            var matching = allBindings.Where(x => StructuralComparisons.StructuralEqualityComparer.Equals(x.CertificateHash, newCert)).ToList();
            var created = matching.Where(x => x.Id == 0);
            var updated = matching.Except(created);
            var unchanged = allBindings.Except(matching);

            var updatedFormat = string.Join(",", updated.Select(x => x.Id).Order());
            var notUpdatedFormat = string.Join(",", unchanged.Select(x => x.Id).Order());
            var shouldBeUpdatedFormat = string.Join(",", shouldBeUpdated.Select(x => x.Id).Order());
            var shouldNotBeUpdatedFormat = string.Join(",", shouldNotBeUpdated.Select(x => x.Id).Order());

            Assert.AreEqual(shouldBeUpdatedFormat, updatedFormat, "Updated bindings do not match expected");
            Assert.AreEqual(shouldNotBeUpdatedFormat, notUpdatedFormat, "Not updated bindings do not match expected");
           
            Assert.AreEqual(newBindingsExpected.Count(), created.Count());
            Assert.AreEqual(newBindingsExpected.Count(), ctx.AddedBindings.Count);
        }
    }
}