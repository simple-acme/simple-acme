using ACMESharp.Authorizations;
using PKISharp.WACS.Context;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    [IPlugin.Plugin1<
        RestOptions, RestOptionsFactory, 
        HttpValidationCapability, RestJson, RestArguments>
        ("11ba2994-ea59-4f2f-b9eb-0eaa2fa3cbfa", 
        "REST", "Send verification files to the server by issuing an http(s) request", "REST request")]
    internal sealed class Rest(
        IProxyService proxyService,
        ILogService log,
        SecretServiceManager ssm,
        RestOptions options) : Validation<Http01ChallengeValidationDetails>
    {
        private readonly ConcurrentBag<(string url, string challengeValue)> _urlsChallenges = [];
        private readonly string? _securityToken = ssm.EvaluateSecret(options.SecurityToken);
        private readonly bool _useHttps = options.UseHttps == true;

        public override ParallelOperations Parallelism => ParallelOperations.Prepare | ParallelOperations.Answer;

        public override Task PrepareChallenge(ValidationContext context, Http01ChallengeValidationDetails challenge)
        {
            var resourceUrl = challenge.HttpResourceUrl;
            if (_useHttps)
            {
                resourceUrl = resourceUrl.Replace("http://", "https://");
            }
            _urlsChallenges.Add((resourceUrl, challenge.HttpResourceValue));
            return Task.CompletedTask;
        }

        public override async Task Commit()
        {
            log.Information("Sending verification files to the server(s)");

            using var client = GetClient();

            var responses = await Task.WhenAll(_urlsChallenges
                .Select(item => client.PutAsync(item.url, new StringContent(item.challengeValue))));

            var isError = false;
            foreach (var resp in responses.Where(r => !r.IsSuccessStatusCode))
            {
                isError = true;
                log.Error("Error {ErrorCode} sending verification file to server {Server}", resp.StatusCode, resp.RequestMessage?.RequestUri?.Host);
            }
            if (isError)
            {
                throw new Exception("Failure sending verification files to one or more servers");
            }
        }

        public override async Task CleanUp()
        {
            log.Information("Removing verification files from the server(s)");

            using var client = GetClient();

            var responses = await Task.WhenAll(_urlsChallenges
                .Select(item => client.DeleteAsync(item.url)));
            
            foreach (var resp in responses.Where(r => !r.IsSuccessStatusCode))
            {
                log.Warning("Error {ErrorCode} removing verification file from server {Server}", resp.StatusCode, resp.RequestMessage?.RequestUri?.Host);
            }
        }

        private HttpClient GetClient()
        {
            var client = proxyService.GetHttpClient(false);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _securityToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }
    }
}
