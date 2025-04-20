using ACMESharp.Authorizations;
using ACMESharp.Protocol.Resources;
using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Context;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.ValidationPlugins.Http;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Any
{
    [IPlugin.Plugin<
        ManualOptions, PluginOptionsFactory<ManualOptions>,
        ManualValidationCapability, WacsJsonPlugins>
        ("e45d62b9-f9a8-441e-b95f-c5ee0dcd8040",
        "Manual", "Perform validation challenge manually (auto-renew not possible)")]
    internal class Manual(
        LookupClientProvider dnsClient,
        HttpValidationParameters pars,
        IInputService input,
        ILogService log,
        ISettingsService settings) : IValidationPlugin
    {
        private ManualDns? _manualDns;
        private ManualHttp? _manualHttp;

        /// <summary>
        /// User can prepare multiple challenges before proceeding to validation
        /// </summary>
        public ParallelOperations Parallelism => ParallelOperations.Answer;

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
                _manualDns = new ManualDns(dnsClient, log, input, settings);
                return await _manualDns.PrepareChallenge(context, dnsChallenge);
            }
            else if (context.ChallengeDetails is Http01ChallengeValidationDetails httpChallenge)
            {
                _manualHttp = new ManualHttp(pars, pars.RunLevel, input);
                return await _manualHttp.PrepareChallenge(context, httpChallenge);
            }
            return false;
        }

        public async Task CleanUp() =>
            await (
                _manualDns?.CleanUp() ??
                _manualHttp?.CleanUp() ??
                Task.CompletedTask);

        public Task Commit() => Task.CompletedTask;
    }
}
