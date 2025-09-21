using ACMESharp.Authorizations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using PKISharp.WACS.Context;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    [IPlugin.Plugin1<
        SelfHostingOptions, SelfHostingOptionsFactory, 
        SelfHostingCapability, WacsJsonPlugins, SelfHostingArguments>
        ("c7d5e050-9363-4ba1-b3a8-931b31c618b7", 
        "SelfHosting", "Let simple-acme answer HTTP validation request", 
        Name = "Self-hosting")]
    internal class SelfHosting(ILogService log, RunLevel runLevel, IInputService input, SelfHostingOptions options) : 
        HttpValidationBase(log, runLevel, input)
    {
        internal const int DefaultHttpValidationPort = 80;
        internal const int DefaultHttpsValidationPort = 443;

        private WebApplication? _listener;
        private readonly ConcurrentDictionary<string, string> _files = new();

        /// <summary>
        /// We can answer requests for multiple domains
        /// </summary>
        public override ParallelOperations Parallelism => ParallelOperations.Answer;

        private bool HasListener => _listener != null;
        private WebApplication Listener
        {
            get
            {
                if (_listener == null)
                {
                    throw new InvalidOperationException("Listener not present");
                }
                return _listener;
            }
            set => _listener = value;
        }

        private async Task ReceiveRequest(HttpContext context, string path)
        {
            if (_files.TryGetValue(path, out var response))
            {
                log.Verbose("Serving file {name} to {ip}", path, context.Connection.RemoteIpAddress);
                await context.Response.WriteAsync(response);
            }
            else
            {
                log.Warning("Couldn't serve file {name}", path);
                context.Response.StatusCode = 404;
            }
        }

        public override async Task<bool> PrepareChallenge(ValidationContext context, Http01ChallengeValidationDetails challenge)
        {
            // Add validation file
            _files.GetOrAdd(challenge.HttpResourceName, challenge.HttpResourceValue);

            if (_listener == null)
            {
                var port = DefaultHttpValidationPort;
                try
                {
                    var (listener, listenerPort) = CreateFromOptions(options);
                    if (OperatingSystem.IsWindows())
                    {
                        listener.MapGet($"{{*path}}", ReceiveRequest);
                    }
                    else
                    {
                        listener.MapGet($"/{Http01ChallengeValidationDetails.HttpPathPrefix}/{{*path}}", ReceiveRequest);
                    }
                    await listener.StartAsync();
                    port = listenerPort;
                    Listener = listener;
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Unable to activate listener on port {port}", port);
                    throw;
                }
            }

            await TestChallenge(challenge);
            return true;
        }

        public override Task Commit() => Task.CompletedTask;

        private static (WebApplication, int) CreateListener(bool? https, int? userPort)
        {
            var protocol = https == true ? "https" : "http";           
            var port = userPort ?? ((https == true) ? DefaultHttpsValidationPort : DefaultHttpValidationPort);
            var builder = WebApplication.CreateSlimBuilder();
            builder.Logging.ClearProviders();
            if (OperatingSystem.IsWindows())
            {
                builder.WebHost.UseHttpSys(options => options.UrlPrefixes.Add($"{protocol}://+:{port}/{Http01ChallengeValidationDetails.HttpPathPrefix}/"));
            }
            else
            {
                builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(port));
            }
            var app = builder.Build();
            return (app, port);
        }

        public static (WebApplication, int) CreateFromOptions(SelfHostingOptions args) => CreateListener(args.Https, args.Port);

        public override async Task CleanUp()
        {
            // Cleanup listener if nobody else has done it yet
            if (HasListener)
            {
                try
                {
                    await Listener.StopAsync();
                    await Listener.DisposeAsync();
                }
                finally
                {
                    _listener = null;
                }
            }
        }
    }
}
