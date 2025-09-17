using ACMESharp.Authorizations;
using ACMESharp.Protocol.Resources;
using PKISharp.WACS.Clients;
using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Context;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.ValidationPlugins.Http;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Any
{
    [IPlugin.Plugin<
        ScriptOptions, ScriptOptionsFactory,
        ScriptValidationCapability, WacsJsonPlugins>
        ("8f1da72e-f727-49f0-8546-ef69e5ecec32",
        "DnsScript", "Perform validation challenge with your own script",
        Hidden = true)]
    [IPlugin.Plugin1<
        ScriptOptions, ScriptOptionsFactory,
        ScriptValidationCapability, WacsJsonPlugins, ScriptArguments>
        ("8f1da72e-f727-49f0-8546-ef69e5ecec32",
        "Script", "Perform validation challenge with your own script",
        Name = "Custom script")]
    internal class Script(
        ScriptOptions options,
        LookupClientProvider dnsClient,
        ILogService log,
        ScriptClient client,
        SecretServiceManager secretServiceManager,
        DomainParseService domainParseService,
        HttpValidationParameters pars,
        ISettings settings) : IValidationPlugin
    {
        private ScriptDns? _scriptDns;
        private ScriptHttp? _scriptHttp;

        /// <summary>
        /// User can prepare multiple challenges before proceeding to validation
        /// </summary>
        public ParallelOperations Parallelism => (ParallelOperations)(options.Parallelism ?? 0);

        /// <summary>
        /// Pick challenge type based on user input or configuration
        /// </summary>
        /// <param name="supportedChallenges"></param>
        /// <returns></returns>
        public Task<AcmeChallenge?> SelectChallenge(List<AcmeChallenge> supportedChallenges) => 
            Task.FromResult(supportedChallenges.FirstOrDefault(c => c.Type == (options.ChallengeType ?? Constants.Dns01ChallengeType)));

        /// <summary>
        /// Prepare a single challenge for validation
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task<bool> PrepareChallenge(ValidationContext context)
        {
            if (context.ChallengeDetails is Dns01ChallengeValidationDetails dnsChallenge)
            {
                _scriptDns = new ScriptDns(this, dnsClient, domainParseService, log, settings);
                return await _scriptDns.PrepareChallenge(context, dnsChallenge);
            }
            else if (context.ChallengeDetails is Http01ChallengeValidationDetails httpChallenge)
            {
                _scriptHttp = new ScriptHttp(this, context, pars, httpChallenge);
                return await _scriptHttp.PrepareChallenge(context, httpChallenge);
            }
            return false;
        }

        /// <summary>
        /// Common create for DNS/HTTP challenges
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="domain"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal async Task<bool> Create(string identifier, string domain, string value)
        {
            var script = options.Script ?? options.CreateScript;
            if (!string.IsNullOrWhiteSpace(script))
            {
                var args = options.ChallengeType == Constants.Http01ChallengeType ?
                    ScriptHttp.DefaultPrepareArguments :
                    ScriptDns.DefaultPrepareArguments;
                if (!string.IsNullOrWhiteSpace(options.CreateScriptArguments))
                {
                    args = options.CreateScriptArguments;
                }
                var escapeToken = script.EndsWith(".ps1");
                var actualArguments = await ProcessArguments(identifier, domain, value, args, escapeToken, false);
                var censoredArguments = await ProcessArguments(identifier, domain, value, args, escapeToken, true);
                var result = await client.RunScript(script, actualArguments, censoredArguments);
                return result.Success;
            }
            else
            {
                log.Error("No prepare script configured");
                return false;
            }
        }

        /// <summary>
        /// Common delete for DNS/HTTP challenges
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="domain"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        internal async Task Delete(string identifier, string domain, string value)
        {
            var script = options.Script ?? options.DeleteScript;
            if (!string.IsNullOrWhiteSpace(script))
            {
                var args = options.ChallengeType == Constants.Http01ChallengeType ? 
                    ScriptHttp.DefaultCleanupArguments : 
                    ScriptDns.DefaultCleanupArguments;
                if (!string.IsNullOrWhiteSpace(options.DeleteScriptArguments))
                {
                    args = options.DeleteScriptArguments;
                }
                var escapeToken = script.EndsWith(".ps1");
                var actualArguments = await ProcessArguments(identifier, domain, value, args, escapeToken, false);
                var censoredArguments = await ProcessArguments(identifier, domain, value, args, escapeToken, true);
                await client.RunScript(script, actualArguments, censoredArguments);
            }
            else
            {
                log.Warning("No cleanup script configured");
            }
        }

        /// <summary>
        /// Run argument replacements
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="recordName"></param>
        /// <param name="token"></param>
        /// <param name="args"></param>
        /// <param name="escapeToken"></param>
        /// <param name="censor"></param>
        /// <returns></returns>
        private async Task<string> ProcessArguments(string identifier, string path, string token, string args, bool escapeToken, bool censor)
        {
            // Some tokens start with - which confuses Powershell. We did not want to 
            // make a breaking change for .bat or .exe files, so instead escape the 
            // token with double quotes, as Powershell discards the quotes anyway and 
            // thus it's functionally equivalant.
            if (escapeToken && (args.Contains(" {Token} ") || args.EndsWith(" {Token}")))
            {
                args = args.Replace("{Token}", "\"{Token}\"");
            }

            // Replace tokens in the script
            var replacements = options.ChallengeType == Constants.Http01ChallengeType
                ? _scriptHttp?.ReplaceTokens(identifier, path, censor, token)
                : _scriptDns?.ReplaceTokens(identifier, path, censor, token);

            return await ScriptClient.ReplaceTokens(args, replacements ?? [], secretServiceManager, censor);
        }

        public async Task CleanUp() =>
            await (
                _scriptDns?.CleanUp() ?? 
                _scriptHttp?.CleanUp() ??
                Task.CompletedTask);

        public Task Commit() => Task.CompletedTask;
    }
}
