using ACMESharp.Authorizations;
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

        private async Task ReceiveRequests()
        {
            while (HasListener && Listener.IsListening)
            {
                var ctx = await Listener.GetContextAsync();
                var path = ctx.Request.Url?.LocalPath ?? "";
                if (_files.TryGetValue(path, out var response))
                {
                    log.Verbose("SelfHosting plugin serving file {name}", path);
                    using var writer = new StreamWriter(ctx.Response.OutputStream);
                    writer.Write(response);
                }
                else
                {
                    log.Warning("SelfHosting plugin couldn't serve file {name}", path);
                    ctx.Response.StatusCode = 404;
                }
            }
        }

        public override async Task<bool> PrepareChallenge(ValidationContext context, Http01ChallengeValidationDetails challenge)
        {
            // Add validation file
            _files.GetOrAdd("/" + challenge.HttpResourcePath, challenge.HttpResourceValue);
            await TestChallenge(challenge);
            return true;
        }

        public override Task Commit()
        {
            // Create listener if it doesn't exist yet
            lock (_listenerLock)
            {
                if (_listener == null)
                {
                    var port = DefaultHttpValidationPort; 
                    try
                    {
                        var (listener, listenerPort) = CreateFromOptions(options);
                        port = listenerPort;
                        listener.Start();
                        Listener = listener;
                        Task.Run(ReceiveRequests);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex, "Unable to activate listener on port {port}", port);
                        throw;
                    }
                }
            }
            return Task.CompletedTask;
        }

        private static (HttpListener, int) CreateListener(bool? https, int? userPort)
        {
            var protocol = https == true ? "https" : "http";
            var port = userPort ?? ((https == true) ?
                DefaultHttpsValidationPort :
                DefaultHttpValidationPort);
            var prefix = $"{protocol}://+:{port}/.well-known/acme-challenge/";
            var testListener = new HttpListener();
            testListener.Prefixes.Add(prefix);
            return (testListener, port);
        }

        public static (HttpListener, int) CreateFromOptions(SelfHostingOptions args) => CreateListener(args.Https, args.Port);

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
