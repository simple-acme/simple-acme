using ACMESharp.Authorizations;
using ACMESharp.Protocol.Resources;
using PKISharp.WACS.Clients;
using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Context;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Any
{
    [IPlugin.Plugin<
        ScriptOptions, ScriptOptionsFactory,
        DnsValidationCapability, WacsJsonPlugins>
        ("8f1da72e-f727-49f0-8546-ef69e5ecec32",
        "DnsScript", "Create verification records with your own script",
        Hidden = true)]
    [IPlugin.Plugin1<
        ScriptOptions, ScriptOptionsFactory,
        DnsValidationCapability, WacsJsonPlugins, ScriptArguments>
        ("8f1da72e-f727-49f0-8546-ef69e5ecec32",
        "Script", "Create verification records with your own script",
        Name = "Custom script")]
    internal class Script(
        ScriptOptions options,
        LookupClientProvider dnsClient,
        IInputService input,
        ILogService log,
        ScriptClient client,
        SecretServiceManager secretServiceManager,
        DomainParseService domainParseService,
        ISettings settings) : IValidationPlugin
    {
        internal const string DefaultCreateArguments = "create {Identifier} {RecordName} {Token}";
        internal const string DefaultDeleteArguments = "delete {Identifier} {RecordName} {Token}";

        private ScriptDns? _scriptDns;

        /// <summary>
        /// User can prepare multiple challenges before proceeding to validation
        /// </summary>
        public ParallelOperations Parallelism => (ParallelOperations)(options.Parallelism ?? 0);

        /// <summary>
        /// Have the user choose the challenge type that they want to handle
        /// </summary>
        /// <param name="supportedChallenges"></param>
        /// <returns></returns>
        public async Task<AcmeChallenge?> SelectChallenge(List<AcmeChallenge> supportedChallenges)
        {
            if (supportedChallenges.Count == 1)
            {
                return supportedChallenges[0];
            }
            return await input.ChooseRequired(
                "How would you like to prove that you own the domain?",
                supportedChallenges, c => Choice.Create(c,
                   c.Type switch
                   {
                       Constants.Dns01ChallengeType => "Create a DNS record",
                       Constants.Http01ChallengeType => "Upload a file",
                       _ => "Unknown (bug?)"
                   }));
        }

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
                _scriptDns = new ScriptDns(options, dnsClient, client, log, secretServiceManager, domainParseService, settings);
                return await _scriptDns.PrepareChallenge(context, dnsChallenge);
            }
            else if (context.ChallengeDetails is Http01ChallengeValidationDetails)
            {
                throw new NotImplementedException();
            }
            return false;
        }

        public async Task CleanUp() =>
            await (
                _scriptDns?.CleanUp() ??
                Task.CompletedTask);

        public Task Commit() => Task.CompletedTask;
    }
}
