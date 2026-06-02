using ACMESharp.Authorizations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PKISharp.WACS.Services;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    public class KestrelSelfHoster : ISelfHoster
    {
        private readonly WebApplication _app;
        private readonly ILogService _log;

        public KestrelSelfHoster(ISelfHosterOptions options, ILogService log)
        {
            _log = log;
            var https = options.Https == true;
            var protocol = https ? "https" : "http";
            Port = options.Port ?? (https ? 443 : 80);
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            if (OperatingSystem.IsWindows())
            {
                builder.WebHost.UseHttpSys(options => options.UrlPrefixes.Add($"{protocol}://+:{Port}/{Http01ChallengeValidationDetails.HttpPathPrefix}/"));
            }
            else
            {
                builder.WebHost.ConfigureKestrel(options => {
                    options.ListenAnyIP(Port, listenOptions =>
                    {
                        if (https)
                        {
                            using var rsa = RSA.Create(2048);
                            var name = new X500DistinguishedName($"CN=www.example.com");
                            var request = new CertificateRequest(name, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                            var now = DateTime.UtcNow;
                            var certificate = request.CreateSelfSigned(new DateTimeOffset(now.AddDays(-1)), new DateTimeOffset(now.AddDays(1)));
                            listenOptions.UseHttps(certificate);
                        }
                    });
                });
            }
            _app = builder.Build();
            if (OperatingSystem.IsWindows())
            {
                _app.MapGet($"{{*path}}", ReceiveRequest);
            }
            else
            {
                _app.MapGet($"/{Http01ChallengeValidationDetails.HttpPathPrefix}/{{*path}}", ReceiveRequest);
            }
        }

        private async Task ReceiveRequest(HttpContext context, string path)
        {
            if (Challenges.TryGetValue(path, out var response))
            {
                _log.Verbose("Serving file {name} to {ip}", path, context.Connection.RemoteIpAddress);
                await context.Response.WriteAsync(response);
            }
            else
            {
                _log.Warning("Couldn't serve file {name}", path);
                context.Response.StatusCode = 404;
            }
        }

        public ConcurrentDictionary<string, string> Challenges { get; set; } = new();
        public int Port { get; }
        public bool Started { get; private set; }

        public async Task Start()
        {
            await _app.StartAsync();
            Started = true;
        }

        public async Task Stop()
        {
            if (Started)
            {
                await _app.StopAsync();
            }
            await _app.DisposeAsync();
        }
    }
}
