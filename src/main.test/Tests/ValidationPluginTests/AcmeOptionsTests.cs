using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.DomainObjects.Identifiers;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PKISharp.WACS.UnitTests.Tests.ValidationPluginTests
{
    [TestClass]
    public class AcmeOptionsTests
    {
        private const string Server = "https://acme-dns.example/";

        [TestMethod]
        [DataRow("user", "abc+/xyz")]
        [DataRow("user", "literal%2F")]
        [DataRow("user", "vault://mock/key1")]
        [DataRow("user", null)]
        public async Task DefaultReadsOptionalCredentials(string userName, string? password)
        {
            using var context = new AcmeDnsTestContext();
            var args = new[] { "--acmednsserver", Server, "--username", userName };
            if (password != null)
            {
                args = [.. args, "--password", password];
            }
            var options = await context.Factory(args).Default();
            Assert.IsNotNull(options);
            Assert.IsNotNull(options.Credential);
            Assert.AreEqual(userName, options.Credential.UserName);
            Assert.AreEqual(password, options.Credential.Password?.Value);
        }

        [TestMethod]
        public async Task DefaultWithoutCredentialsKeepsLegacyConfiguration()
        {
            using var context = new AcmeDnsTestContext();
            var options = await context.Factory(["--acmednsserver", Server]).Default();
            Assert.IsNotNull(options);
            Assert.AreEqual(Server, options.BaseUri);
            Assert.IsNull(options.Credential);
        }

        [TestMethod]
        [DataRow("", "password")]
        [DataRow("user:invalid", "password")]
        public async Task InvalidCredentialsAreRejected(string userName, string password)
        {
            using var context = new AcmeDnsTestContext();
            var factory = context.Factory(["--acmednsserver", Server, "--username", userName, "--password", password]);
            await Assert.ThrowsAsync<ArgumentException>(() => factory.Default());
        }

        [TestMethod]
        public async Task InteractiveAuthenticationIsOptional()
        {
            using var context = new AcmeDnsTestContext([Server, "n"]);
            var options = await context.Factory([]).Aquire(context.Input, RunLevel.Interactive);
            Assert.IsNotNull(options);
            Assert.IsNull(options.Credential);
            Assert.HasCount(1, context.Input.Questions);
            Assert.IsFalse(context.Input.Questions[0].Default);
        }

        [TestMethod]
        public async Task InteractiveAuthenticationUsesSharedSecretInput()
        {
            using var context = new AcmeDnsTestContext([Server, "y", "user", "abc+/xyz", "n"]);
            var options = await context.Factory([]).Aquire(context.Input, RunLevel.Interactive);
            Assert.IsNotNull(options);
            Assert.IsNotNull(options.Credential);
            Assert.AreEqual("user", options.Credential.UserName);
            Assert.AreEqual("abc+/xyz", options.Credential.Password?.Value);
        }

        [TestMethod]
        public async Task InteractiveLegacyUrlDoesNotPromptForSeparateCredentials()
        {
            const string legacyServer = "https://user:abc%2B%2Fxyz@acme-dns.example/";
            using var context = new AcmeDnsTestContext([legacyServer]);
            var options = await context.Factory([]).Aquire(context.Input, RunLevel.Interactive);
            Assert.IsNotNull(options);
            Assert.AreEqual(legacyServer, options.BaseUri);
            Assert.IsNull(options.Credential);
            Assert.IsEmpty(context.Input.Questions);
        }

        [TestMethod]
        public async Task FactoryUsesCliCredentialsForRegistration()
        {
            using var context = new AcmeDnsTestContext([""]);
            var factory = context.Factory(["--acmednsserver", Server, "--username", "user", "--password", "abc+/xyz"],
                new Target(new DnsIdentifier("example.com")));
            // The fake server returns null after capturing the registration request.
            Assert.IsNull(await factory.Aquire(context.Input, RunLevel.Interactive));
            Assert.AreEqual("/register", context.Handler.RequestUri?.AbsolutePath);
            AcmeDnsTests.AssertAuthorization(context.Handler, "user:abc+/xyz");
            Assert.IsEmpty(context.Input.Questions);
        }

        [TestMethod]
        [DataRow("abc+/xyz")]
        [DataRow("vault://mock/key1")]
        public async Task CredentialsSurviveReloadAndAreUsedForUpdates(string password)
        {
            using var context = new AcmeDnsTestContext();
            var options = await context.Factory(["--acmednsserver", Server, "--username", "user", "--password", password]).Default();
            Assert.IsNotNull(options);
            var jsonContext = new AcmeJson(new WacsJsonPluginsOptionsFactory(context.Log, context.Settings));
            var json = JsonSerializer.Serialize(options, jsonContext.AcmeOptions);
            Assert.Contains("\"PasswordSafe\"", json);
            if (!password.StartsWith(SecretServiceManager.VaultPrefix))
            {
                Assert.DoesNotContain(password, json);
            }
            var restored = JsonSerializer.Deserialize(json, jsonContext.AcmeOptions);
            Assert.IsNotNull(restored);
            Assert.IsNotNull(restored.Credential);
            Assert.AreEqual(password, restored.Credential.Password?.Value);
            var uri = new Uri(restored.BaseUri!);
            await context.WriteRegistration(uri);
            Assert.IsTrue(await context.Client(uri, restored.Credential).Update("example.com", "token"));
            AcmeDnsTests.AssertAuthorization(context.Handler, password.StartsWith(SecretServiceManager.VaultPrefix)
                ? "user:secret1" : $"user:{password}");
        }

        [TestMethod]
        public void OldOptionsWithoutCredentialStillDeserialize()
        {
            var options = JsonSerializer.Deserialize("""{"BaseUri":"https://acme-dns.example/"}""", AcmeJson.Default.AcmeOptions);
            Assert.IsNotNull(options);
            Assert.AreEqual(Server, options.BaseUri);
            Assert.IsNull(options.Credential);
        }

        [TestMethod]
        public async Task PasswordIsMarkedSecretAndMaskedInDescriptions()
        {
            using var context = new AcmeDnsTestContext();
            var factory = context.Factory(["--acmednsserver", Server, "--username", "user", "--password", "abc+/xyz"]);
            var options = await factory.Default();
            Assert.IsNotNull(options);
            var description = factory.Describe(options).Single(item => item.Item1.ArgumentName == "password");
            Assert.IsTrue(description.Item1.Secret);
            Assert.IsInstanceOfType<ProtectedString>(description.Item2);
            Assert.AreEqual("********", ((ProtectedString)description.Item2).DisplayValue);
            options.Show(context.Input);
            Assert.Contains("user", context.Input.ShownValues);
            Assert.Contains("********", context.Input.ShownValues);
            Assert.DoesNotContain("abc+/xyz", context.Input.ShownValues);
        }
    }
}
