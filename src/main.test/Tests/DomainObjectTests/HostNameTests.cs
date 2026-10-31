
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.DomainObjects;

namespace PKISharp.WACS.UnitTests.Tests.DomainObjectTests
{
    [TestClass]
    public class HostNameTests
    {
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
            Assert.AreEqual(HostName.Parse(a), HostName.Parse(b));
        }
    }
}
