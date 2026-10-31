
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.DomainObjects;
using System.Collections.Generic;

namespace PKISharp.WACS.UnitTests.Tests.DomainObjectTests
{
    [TestClass]
    public class IdentifierTests
    {
        [TestMethod]
        public void EqualityTests()
        {
            Assert.AreEqual(new DnsIdentifier("a.com"), new DnsIdentifier("a.com"));
            Assert.AreEqual(new DnsIdentifier("A.com"), new DnsIdentifier("a.COM"));
            Assert.AreNotEqual(new DnsIdentifier("a.com"), new DnsIdentifier("b.com"));
            Assert.AreNotEqual<Identifier>(new DnsIdentifier("a.com"), new UpnIdentifier("a.com"));

            var list = new List<Identifier>() { new DnsIdentifier("a.com") };
            Assert.Contains(new DnsIdentifier("a.com"), list);
            Assert.Contains(new DnsIdentifier("a.COM"), list);
            Assert.DoesNotContain(new UpnIdentifier("a.com"), list);

            var sortable = new List<Identifier>() { new DnsIdentifier("b.com"), new DnsIdentifier("a.com") };
            sortable.Sort();
            Assert.AreEqual(sortable[0], new DnsIdentifier("a.com"));
        }

        [TestMethod]
        [DataRow("a.com", "a.com", DisplayName = "Regular")]
        [DataRow("A.com", "a.com", DisplayName = "Case insensitive for ASCII")]
        [DataRow("xn--c1yn36f.com", "xn--c1yn36f.com", DisplayName = "Punycode to punycode")]
        [DataRow("點看.com", "xn--c1yn36f.com", DisplayName = "Punycode and Unicode are equivalent")]
        [DataRow("點看.com", "XN--C1YN36F.COM", DisplayName = "Punycode is case insensitive")]
        [DataRow("Ğ.com", "ğ.com", DisplayName = "Case insensitive for Turkish (soft G)")]
        [DataRow("Ğ.com", "xn--tea.com", DisplayName = "Punycode insensitive for Turkish (soft G uppercase)")]
        [DataRow("ğ.com", "xn--tea.com", DisplayName = "Punycode insensitive for Turkish (soft G lowercase)")]
        public void EqualityTests(string a, string b)
        {
            Assert.AreEqual(new DnsIdentifier(a), new DnsIdentifier(b));
        }
    }
}
