using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Configuration;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PKISharp.WACS.UnitTests.Tests.ValidationPluginTests
{
    [TestClass]
    public class AcmeDnsTests
    {
        [TestMethod]
        [DataRow("user:password", "user:password")]
        [DataRow("user:abc%2B%2Fxyz", "user:abc+/xyz")]
        [DataRow("user:abc+%2Fxyz", "user:abc+/xyz")]
        [DataRow("user:with%20space", "user:with space")]
        [DataRow("user:literal%252F", "user:literal%2F")]
        [DataRow("user%40example.com:p%40ss%3Aword%3F%23%25", "user@example.com:p@ss:word?#%")]
        public Task BasicAuthDecodesUrlCredentials(string userInfo, string expectedCredentials) =>
            AssertRequests(new Uri($"https://{userInfo}@acme-dns.example/"), null, expectedCredentials);

        [TestMethod]
        [DataRow("https://acme-dns.example/", "user", "abc+/xyz", "user:abc+/xyz")]
        [DataRow("https://acme-dns.example/", "user", "literal%2F", "user:literal%2F")]
        [DataRow("https://acme-dns.example/", "user@example.com", "p@ss:word?#%", "user@example.com:p@ss:word?#%")]
        [DataRow("https://acme-dns.example/", "gebruiker", "café☃", "gebruiker:café☃")]
        [DataRow("https://acme-dns.example/", "user", null, "user:")]
        [DataRow("https://old:old-password@acme-dns.example/", "new", "new-password", "new:new-password")]
        [DataRow("https://old:old-password@acme-dns.example/", "new", null, "new:")]
        [DataRow("https://acme-dns.example/", "user", "vault://mock/key1", "user:secret1")]
        public Task BasicAuthUsesSeparateCredentials(string url, string userName, string? password, string expectedCredentials) =>
            AssertRequests(new Uri(url), new NetworkCredentialOptions(userName, password), expectedCredentials);

        [TestMethod]
        public Task NoCredentialsOmitsAuthorization() =>
            AssertRequests(new Uri("https://acme-dns.example/"), null, null);

        [TestMethod]
        public async Task VaultPasswordIsResolvedForEachRequest()
        {
            using var context = new AcmeDnsTestContext();
            var uri = new Uri("https://acme-dns.example/");
            await context.WriteRegistration(uri);
            var client = context.Client(uri, new NetworkCredentialOptions("user", "vault://mock/key1"));
            Assert.IsTrue(await client.Update("example.com", "token"));
            AssertAuthorization(context.Handler, "user:secret1");
            await context.SecretStore.PutSecret("key1", "changed+/password");
            Assert.IsTrue(await client.Update("example.com", "another-token"));
            AssertAuthorization(context.Handler, "user:changed+/password");
        }

        private static async Task AssertRequests(Uri uri, NetworkCredentialOptions? credential, string? expectedCredentials)
        {
            foreach (var endpoint in new[] { "register", "update" })
            {
                using var context = new AcmeDnsTestContext();
                if (endpoint == "update")
                {
                    await context.WriteRegistration(uri);
                }
                var client = context.Client(uri, credential);
                if (endpoint == "register")
                {
                    await client.EnsureRegistration("example.com", true);
                }
                else
                {
                    Assert.IsTrue(await client.Update("example.com", "token"));
                    Assert.AreEqual("api-user", context.Handler.ApiUser);
                    Assert.AreEqual("api-key", context.Handler.ApiKey);
                }
                Assert.AreEqual(HttpMethod.Post, context.Handler.Method);
                Assert.AreEqual($"/{endpoint}", context.Handler.RequestUri?.AbsolutePath);
                AssertAuthorization(context.Handler, expectedCredentials);
            }
        }

        internal static void AssertAuthorization(AcmeDnsTestContext.RecordingHandler handler, string? expectedCredentials)
        {
            if (expectedCredentials == null)
            {
                Assert.IsNull(handler.Authorization);
                return;
            }
            Assert.IsNotNull(handler.Authorization);
            Assert.AreEqual("Basic", handler.Authorization.Scheme);
            Assert.IsNotNull(handler.Authorization.Parameter);
            Assert.AreEqual(expectedCredentials,
                Encoding.UTF8.GetString(Convert.FromBase64String(handler.Authorization.Parameter)));
        }
    }
}
