using ACMESharp.Authorizations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PKISharp.WACS.Services;

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
                builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(Port));
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

        public Dictionary<string, string> Challenges { get; set; } = [];
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
