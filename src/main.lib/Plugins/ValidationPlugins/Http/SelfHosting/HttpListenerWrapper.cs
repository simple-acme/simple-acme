using ACMESharp.Authorizations;
using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    internal class HttpListenerWrapper : ISelfHoster
    {
        private readonly HttpListener _listener;
        private readonly ILogService _log;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        public HttpListenerWrapper(ISelfHosterOptions options, ILogService log)
        {
            _log = log;
            var https = options.Https == true;
            var protocol = https ? "https" : "http";
            Port = options.Port ?? (https ? 443 : 80);
            var prefix = $"{protocol}://+:{Port}/.well-known/acme-challenge/";
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
        }

        public Dictionary<string, string> Challenges { get; } = [];
        public int Port { get; }
        public bool Started { get; private set; }

        public Task Start() {
            _listener.Start();
            Started = true;
            Task.Run(ReceiveRequests);
            return Task.CompletedTask;
        }

        public Task Stop() {
            _cancellationTokenSource.Cancel();
            if (Started)
            {
                _listener.Close();
            }
            return Task.CompletedTask;
        }

        private async Task ReceiveRequests()
        {
            while (_listener != null && _listener.IsListening && !_cancellationTokenSource.IsCancellationRequested)
            {
                var ctx = await _listener.GetContextAsync().WaitAsync(_cancellationTokenSource.Token);
                var path = ctx.Request.Url?.LocalPath ?? "";
                var prefix = $"/{Http01ChallengeValidationDetails.HttpPathPrefix}/";
                if (path.StartsWith(prefix))
                {
                    path = path[prefix.Length..];
                }
                if (Challenges.TryGetValue(path, out var response))
                {
                    _log.Verbose("Serving file {name} to {ip}", path, ctx.Request.RemoteEndPoint.Address);
                    using var writer = new StreamWriter(ctx.Response.OutputStream);
                    writer.Write(response);
                }
                else
                {
                    _log.Warning("Couldn't serve file {name}", path);
                    ctx.Response.StatusCode = 404;
                }
            }
        }
    }
}
