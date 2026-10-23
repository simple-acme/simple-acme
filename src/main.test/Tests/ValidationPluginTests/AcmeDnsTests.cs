using Microsoft.VisualStudio.TestTools.UnitTesting;
using PKISharp.WACS.Configuration.Settings;
using PKISharp.WACS.Configuration.Settings.Types;
using PKISharp.WACS.Configuration.Settings.Types.Validation;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns;
using PKISharp.WACS.Services;
using PKISharp.WACS.UnitTests.Mock.Services;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Threading;
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
        public async Task BasicAuthDecodesUrlCredentials(string userInfo, string expectedCredentials)
        {
            foreach (var endpoint in new[] { "register", "update" })
            {
                var configRoot = Directory.CreateTempSubdirectory("acme-dns-test-");
                try
                {
                    var baseUri = new Uri($"https://{userInfo}@acme-dns.example/");
                    var settings = new MockSettingsService(new Settings
                    {
                        Client = new ClientSettings { ConfigurationPath = configRoot.FullName },
                        Validation = new ValidationSettings
                        {
                            Dns = new DnsSettings
                            {
                                PreValidation = new PreValidationSettings { Enabled = false }
                            }
                        }
                    });
                    var registrationPath = Directory.CreateDirectory(Path.Combine(
                        settings.Client.ConfigurationPath, "acme-dns", baseUri.CleanUri()));
                    if (endpoint == "update")
                    {
                        await File.WriteAllTextAsync(Path.Combine(registrationPath.FullName, "example.com.json"),
                            """{"username":"api-user","password":"api-key","subdomain":"subdomain","fulldomain":"subdomain.acme-dns.example"}""");
                    }

                    using var handler = new RecordingHandler();
                    var client = new AcmeDnsClient(null!, new TestProxyService(handler), new Mock.Services.LogService(),
                        settings, new Mock.Services.InputService([]), baseUri);
                    if (endpoint == "register")
                    {
                        await client.EnsureRegistration("example.com", true);
                    }
                    else
                    {
                        Assert.IsTrue(await client.Update("example.com", "token"));
                        Assert.AreEqual("api-user", handler.ApiUser);
                        Assert.AreEqual("api-key", handler.ApiKey);
                    }

                    Assert.AreEqual(HttpMethod.Post, handler.Method);
                    Assert.AreEqual($"/{endpoint}", handler.RequestUri?.AbsolutePath);
                    Assert.IsNotNull(handler.Authorization);
                    Assert.AreEqual("Basic", handler.Authorization.Scheme);
                    Assert.IsNotNull(handler.Authorization.Parameter);
                    Assert.AreEqual(expectedCredentials,
                        Encoding.ASCII.GetString(Convert.FromBase64String(handler.Authorization.Parameter)), endpoint);
                }
                finally
                {
                    configRoot.Delete(true);
                }
            }
        }

        private class RecordingHandler : HttpMessageHandler
        {
            public AuthenticationHeaderValue? Authorization { get; private set; }
            public HttpMethod? Method { get; private set; }
            public Uri? RequestUri { get; private set; }
            public string? ApiUser { get; private set; }
            public string? ApiKey { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Authorization = request.Headers.Authorization;
                Method = request.Method;
                RequestUri = request.RequestUri;
                ApiUser = request.Headers.Contains("X-Api-User") ? string.Join("", request.Headers.GetValues("X-Api-User")) : null;
                ApiKey = request.Headers.Contains("X-Api-Key") ? string.Join("", request.Headers.GetValues("X-Api-Key")) : null;
                // Stop registration after the HTTP request, without performing DNS verification.
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null") });
            }
        }

        private class TestProxyService(HttpMessageHandler handler) : IProxyService
        {
            public SslProtocols SslProtocols { get; set; }
            public WindowsProxyUsePolicy ProxyType => throw new NotImplementedException();
            public Task<HttpClient> GetHttpClient(bool checkSsl = true) => Task.FromResult(new HttpClient(handler, false));
            public Task<HttpMessageHandler> GetHttpMessageHandler() => throw new NotImplementedException();
            public Task<IWebProxy?> GetWebProxy() => throw new NotImplementedException();
            public void Disable() => throw new NotImplementedException();
        }
    }
}
