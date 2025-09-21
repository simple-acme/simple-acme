using ACMESharp.Authorizations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using PKISharp.WACS.Context;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
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

        private readonly object _listenerLock = new();
        private HttpListener? _listener;
        private readonly ConcurrentDictionary<string, string> _files = new();

        /// <summary>
        /// We can answer requests for multiple domains
        /// </summary>
        public override ParallelOperations Parallelism => ParallelOperations.Answer | ParallelOperations.Prepare;

        private bool HasListener => _listener != null;
        private HttpListener Listener
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
                log.Verbose("SelfHosting plugin serving file {name} to {ip}", path, context.Connection.RemoteIpAddress);
                await context.Response.WriteAsync(response);
            }
            else
            {
                log.Warning("SelfHosting plugin couldn't serve file {name}", path);
                context.Response.StatusCode = 404;
            }
        }

        public override async Task<bool> PrepareChallenge(ValidationContext context, Http01ChallengeValidationDetails challenge)
        {
            // Add validation file
            _files.GetOrAdd(challenge.HttpResourceName, challenge.HttpResourceValue);
            await TestChallenge(challenge);
            return true;
        }

        public override async Task Commit()
        {
            // Create listener if it doesn't exist yet
            // Create listener if it doesn't exist yet
            lock (_listenerLock)
            {
                if (_listener == null)
                {
                    var port = DefaultHttpValidationPort;
                    try
                    {
                        var (listener, listenerPort) = CreateFromOptions(options);
                        listener.MapGet($"/{Http01ChallengeValidationDetails.HttpPathPrefix}/{{*path}}", ReceiveRequest);
                        listener.Start();
                        port = listenerPort;
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex, "Unable to activate listener on port {port}", port);
                        throw;
                    }
                }
            }
        }

        private static (WebApplication, int) CreateListener(bool? https, int? userPort)
        {
            var protocol = https == true ? "https" : "http";            var port = userPort ?? ((https == true) ? DefaultHttpsValidationPort : DefaultHttpValidationPort);
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.ConfigureKestrel(options => options.ListenAnyIP(port));
            var app = builder.Build();
            return (app, port);
        }

        public static (WebApplication, int) CreateFromOptions(SelfHostingOptions args) => CreateListener(args.Https, args.Port);

        public override Task CleanUp()
        {
            // Cleanup listener if nobody else has done it yet
            lock (_listenerLock)
            {
                if (HasListener)
                {
                    try
                    {
                        Listener.Stop();
                        Listener.Close();
                    }
                    finally
                    {
                        _listener = null;
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}
