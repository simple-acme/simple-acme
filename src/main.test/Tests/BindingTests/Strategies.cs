using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.UnitTests.Mock.Clients;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace PKISharp.WACS.UnitTests.Tests.BindingTests
{
    [TestClass]
    [SuppressMessage("Performance", "CA1861:Avoid constant arrays as arguments", Justification = "Not supported by decorators")]
    public class BindingStrategyTests
    {
        private readonly Mock.Services.LogService log;
        private static readonly byte[] newCert = [0x2];
        private const byte oldCertByte = 0x1;
        private static readonly byte[] oldCert1 = [oldCertByte];
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
                },
                new MockSite() {
                    Id = 4,
                    Bindings = [
                        new() {
                            Id = 12,
                            IP = "192.168.100.200",
                            CertificateHash = oldCert1,
                            Port = 443,
                            Protocol = "https"
                        },                        
                        new() {
                            Id = 13,
                            IP = "*",
                            Port = 4443,
                            Protocol = "https"
                        },
                    ]
                },
                new MockSite() {
                    Id = 5,
                    Bindings = [
                        new() {
                            Id = 14,
                            IP = "11.11.11.11",
                            Port = 80,
                            Protocol = "http"
                        }
                    ]
                }
            ]
        };

        [TestMethod]
        [DataRow(new[] { "wouter.tinus.online" }, new[] { 1, 12 }, null,
            new string[] { },
            new byte[] { oldCertByte },
            DisplayName = "All sites")]
        [DataRow(
            new[] { "wouter.tinus.online" },
            new[] { 1, 12 },
            (long)2,
            new string[] { },
            new byte[] { oldCertByte },
            DisplayName = "Other site")]
        [DataRow(
            new[] { "wouter.tinus.online" },
            new[] { 1, 12 },
            (long)1,
            new string[] { },
            new byte[] { oldCertByte },
            DisplayName = "Specific site")]
        public void RegularHostReplace(string[] identifiers, int[] matchesExpected, long? siteId, string[] newBindingsExpected, byte[] oldCert)
        {
            RunTest(identifiers, matchesExpected, siteId, newBindingsExpected, oldCert);
        }

        [TestMethod]
        [DataRow(
            new[] { "wouter.tinus.online" },
            new[] { 1, 2, 3 },
            null,
            new string[] { },
            null,
            DisplayName = "All sites")]
        [DataRow(
            new[] { "wouter.tinus.online" },
            new[] { 3 },
            (long)2,
            new string[] { },
            null,
            DisplayName = "Other site")]
        [DataRow(
            new[] { "wouter.tinus.online" },
            new[] { 1, 2 },
            (long)1,
            new string[] { },
            null,
            DisplayName = "Specific site")]
        public void RegularHostNew(string[] identifiers, int[] matchesExpected, long? siteId, string[] newBindingsExpected, byte[] oldCert)
        {
            RunTest(identifiers, matchesExpected, siteId, newBindingsExpected, oldCert);
        }

        [DataRow(
           new[] { "*.tinus.online" },
           new[] { 1, 4, 12 },
           null,
           new string[] { },
           new byte[] { oldCertByte },
           DisplayName = "All sites")]
        [DataRow(
           new[] { "*.example.com" },
           new[] { 12 },
           null,
           new string[] { },
           new byte[] { oldCertByte },
           DisplayName = "No match")]
        [DataRow(
           new[] { "*.wildcard.example.com" },
           new int[] { 7, 8, 12 },
           null,
           new string[] { },
           new byte[] { oldCertByte },
           DisplayName = "Subdomain match")]
        [DataRow(
            new[] { "*.tinus.online" },
            new[] { 1, 2, 4 },
            (long)1,
            new string[] { },
            null,
            DisplayName = "Specific site")]
        [DataRow(
            new[] { "*.tinus.online" },
            new[] { 1, 4, 12 },
            (long)2,
            new string[] { },
            new byte[] { oldCertByte },
            DisplayName = "Other site")]
        [TestMethod]
        public void WildcardReplace(string[] identifiers, int[] matchesExpected, long? siteId, string[] newBindingsExpected, byte[] oldCert)
        {
            RunTest(identifiers, matchesExpected, siteId, newBindingsExpected, oldCert);
        }

        [DataRow(
           new[] { "*.tinus.online" },
           new[] { 1, 2, 3, 4, 6 },
           null,
           new string[] { },
           null,
           DisplayName = "All sites")]
        [DataRow(
           new[] { "*.example.com" },
           new int[] { },
           null,
           new string[] { },
           null,
           DisplayName = "No match")]
        [DataRow(
           new[] { "*.wildcard.example.com" },
           new int[] { 7, 8 },
           null,
           new string[] { },
           null,
           DisplayName = "Subdomain match")]
        [DataRow(
            new[] { "*.tinus.online" },
            new[] { 1, 2, 4 },
            (long)1,
            new string[] { },
            null,
            DisplayName = "Specific site")]
        [DataRow(
            new[] { "*.tinus.online" },
            new[] { 3, 6 },
            (long)2,
            new string[] { },
            null,
            DisplayName = "Other site")]
        [TestMethod]
        public void WildcardNew(string[] identifiers, int[] matchesExpected, long? siteId, string[] newBindingsExpected, byte[] oldCert)
        {
            RunTest(identifiers, matchesExpected, siteId, newBindingsExpected, oldCert);
        }

        [DataRow(
           new[] { "192.168.100.200" },
           new[] { 12 },
           null,
           new string[] { },
           new byte[] { oldCertByte },
           DisplayName = "All sites")]
        [DataRow(
           new[] { "192.168.100.200" },
            new[] { 12 },
            (long)1,
            new string[] { },
            new byte[] { oldCertByte },
            DisplayName = "Specific site")]
        [DataRow(
            new[] { "192.168.100.200" },
            new[] { 12 },
            (long)2,
            new string[] { },
            new byte[] { oldCertByte },
            DisplayName = "Other site")]
        [TestMethod]
        public void IpReplace(string[] identifiers, int[] matchesExpected, long? siteId, string[] newBindingsExpected, byte[] oldCert)
        {
            RunTest(identifiers, matchesExpected, siteId, newBindingsExpected, oldCert);
        }

        [DataRow(
           new[] { "192.168.100.200" },
           new[] { 12 },
           null,
           new string[] { },
           null,
           DisplayName = "All sites")]
        [DataRow(
            new[] { "192.168.100.200" },
            new int[] { 12 },
            (long)4,
            new string[] { },
            null,
            DisplayName = "Specific site")]
        [DataRow(
            new[] { "192.168.100.200" },
            new int[] { },
            (long)2,
            new string[] { },
            null,
            DisplayName = "Other site")]
        [TestMethod]
        public void IpNew(string[] identifiers, int[] matchesExpected, long? siteId, string[] newBindingsExpected, byte[] oldCert)
        {
            RunTest(identifiers, matchesExpected, siteId, newBindingsExpected, oldCert);
        }

        [DataRow(
            new[] { "adder.tinus.online" },
            new int[] { },
            (long)3,
            new string[] { "*:443:adder.tinus.online (site:3, flags:SNI)" },
            null,
            DisplayName = "Simple add")]
        [DataRow(
            new[] { "duplicate.tinus.com" },
            new int[] { },
            (long)3,
            new string[] { },
            null,
            DisplayName = "Duplicate add")]
        [DataRow(
            new[] { "11.11.11.11" },
            new int[] { },
            (long)5,
            new string[] { "11.11.11.11:443: (site:5, flags:None)" },
            null,
            DisplayName = "IP add")]
        [TestMethod]
        public void Add(string[] identifiers, int[] matchesExpected, long? siteId, string[] newBindingsExpected, byte[] oldCert)
        {
            RunTest(identifiers, matchesExpected, siteId, newBindingsExpected, oldCert);
        }

        public void RunTest(string[] identifiers, int[] matchesExpected, long? siteId, string[] newBindingsExpected, byte[]? oldCert)
        {
            var iis = IIS;

            var allBindings = iis.Sites.SelectMany(x => x.Bindings).ToList();
            var shouldBeUpdated = allBindings.Where(b => matchesExpected.Contains(b.Id)).ToList();
            var shouldNotBeUpdated = allBindings.Except(shouldBeUpdated).ToList();
            CollectionAssert.AreEquivalent(matchesExpected, shouldBeUpdated.Select(x => x.Id).ToList(), "Not all expected bindings found in mock IIS");

            var bindingOptions = new BindingOptions();
            bindingOptions = bindingOptions.WithThumbprint(newCert).WithSiteId(siteId);
            var ctx = iis.UpdateHttpSite(identifiers.Select(Identifier.Parse), bindingOptions, oldCert);

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

            Assert.AreEqual(newBindingsExpected.Length, created.Count(), "Mismatch in new bindings created");
            Assert.AreEqual(newBindingsExpected.Length, ctx.AddedBindings.Count, "Mismatch in new bindings created");
            CollectionAssert.AreEquivalent(newBindingsExpected, ctx.AddedBindings, "Not all expected bindings found in mock IIS");
        }
    }
}