using Autofac;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Settings;
using PKISharp.WACS.Configuration.Settings.Types;
using PKISharp.WACS.Configuration.Settings.Types.Validation;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.DomainObjects.Identifiers;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.ValidationPlugins.Dns;
using PKISharp.WACS.Services;
using PKISharp.WACS.UnitTests.Mock.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace PKISharp.WACS.UnitTests.Tests.ValidationPluginTests
{
    internal sealed class AcmeDnsTestContext : IDisposable
    {
        private static readonly Lazy<AssemblyService> Assemblies = new(() => new TestAssemblyService());
        private readonly DirectoryInfo _configRoot = Directory.CreateTempSubdirectory("acme-dns-test-");
        private readonly IContainer _container;

        public Mock.Services.LogService Log { get; } = new();
        public MockSettingsService Settings { get; }
        public RecordingInputService Input { get; }
        public RecordingHandler Handler { get; } = new();
        public IProxyService Proxy { get; }
        public SecretServiceManager Secrets { get; }
        public Mock.Services.SecretService SecretStore { get; } = new();

        public AcmeDnsTestContext(List<string>? inputs = null)
        {
            Settings = new MockSettingsService(new Settings
            {
                Client = new ClientSettings { ConfigurationPath = _configRoot.FullName },
                Security = new SecuritySettings { EncryptConfig = OperatingSystem.IsWindows() },
                Validation = new ValidationSettings
                {
                    Dns = new DnsSettings { PreValidation = new PreValidationSettings { Enabled = false } }
                }
            });
            Input = new RecordingInputService(inputs ?? []);
            Proxy = new TestProxyService(Handler);
            var builder = new ContainerBuilder();
            builder.RegisterInstance(SecretStore);
            _container = builder.Build();
            Secrets = new SecretServiceManager(_container, Input, new PluginService(Log, Assemblies.Value), Log);
        }

        public AcmeDnsClient Client(Uri uri, NetworkCredentialOptions? credential = null) =>
            new(null!, Proxy, Log, Settings, Input, uri, credential, Secrets);

        public AcmeOptionsFactory Factory(string[] args, Target? target = null)
        {
            var parser = new ArgumentsParser(Log, Assemblies.Value, args);
            var arguments = new ArgumentsInputService(Log, parser, Input, Secrets);
            return new AcmeOptionsFactory(target ?? new Target(null, null, []),
                null!, Log, Settings, Proxy, Secrets, arguments);
        }

        public async Task WriteRegistration(Uri uri)
        {
            var path = Directory.CreateDirectory(Path.Combine(Settings.Client.ConfigurationPath, "acme-dns", uri.CleanUri()));
            await File.WriteAllTextAsync(Path.Combine(path.FullName, "example.com.json"),
                """{"username":"api-user","password":"api-key","subdomain":"subdomain","fulldomain":"subdomain.acme-dns.example"}""");
        }

        public void Dispose()
        {
            Handler.Dispose();
            _container.Dispose();
            _configRoot.Delete(true);
        }

        private sealed class TestAssemblyService : AssemblyService
        {
            public TestAssemblyService() : base(new Mock.Services.LogService())
            {
                // Make the fixture independent of plugins installed beside the test runner.
                _allTypes.Clear();
                _allTypes.AddRange([new(typeof(AcmeArguments)), new(typeof(Mock.Services.SecretService))]);
            }
        }

        internal sealed class RecordingInputService(List<string> inputs) : Mock.Services.InputService(inputs), IInputService
        {
            public List<string?> ShownValues { get; } = [];
            public List<(string Message, bool Default)> Questions { get; } = [];
            public new void Show(string? label, string? value = null, int level = 0) => ShownValues.Add(value);
            public new Task<bool> PromptYesNo(string message, bool defaultOption)
            {
                Questions.Add((message, defaultOption));
                return base.PromptYesNo(message, defaultOption);
            }
        }

        internal sealed class RecordingHandler : HttpMessageHandler
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
                ApiUser = request.Headers.TryGetValues("X-Api-User", out var users) ? users.Single() : null;
                ApiKey = request.Headers.TryGetValues("X-Api-Key", out var keys) ? keys.Single() : null;
                // Stop registration after the HTTP request, without performing DNS verification.
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("null") });
            }
        }

        private sealed class TestProxyService(HttpMessageHandler handler) : IProxyService
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
