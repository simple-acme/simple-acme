using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    internal class AcmeOptionsFactory(
        Target target,
        LookupClientProvider dnsClient,
        ILogService log,
        ISettings settings,
        IProxyService proxy,
        ArgumentsInputService arguments) : PluginOptionsFactory<AcmeOptions>
    {
        private ArgumentResult<string?> Endpoint => arguments.
            GetString<AcmeArguments>(x => x.AcmeDnsServer).
            Validate(x => Task.FromResult(new Uri(x!).ToString() != ""), "invalid uri").
            Required();

        public override async Task<AcmeOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            var ret = new AcmeOptions()
            {
                BaseUri = await Endpoint.Interactive(input).GetValue()
            };
            var acmeDnsClient = new AcmeDnsClient(dnsClient, proxy, log, settings, input, new Uri(ret.BaseUri!));
            var identifiers = target.Parts.SelectMany(x => x.Identifiers).Distinct();
            foreach (var identifier in identifiers)
            {
                var registrationResult = await acmeDnsClient.EnsureRegistration(identifier.Value.Replace("*.", ""), true);
                if (!registrationResult)
                {
                    return null;
                }
            }
            return ret;
        }

        public override async Task<AcmeOptions?> Default()
        {
            var ret = new AcmeOptions()
            {
                BaseUri = await Endpoint.GetValue()
            };
            var acmeDnsClient = new AcmeDnsClient(dnsClient, proxy, log, settings, null, new Uri(ret.BaseUri!));
            var identifiers = target.Parts.SelectMany(x => x.Identifiers).Distinct();
            var valid = true;
            foreach (var identifier in identifiers)
            {
                if (!await acmeDnsClient.EnsureRegistration(identifier.Value.Replace("*.", ""), false))
                {
                    log.Warning("No (valid) acme-dns registration could be found for {identifier}.", identifier);
                    valid = false;
                }
            }
            if (!valid)
            {
                log.Warning($"Creating this renewal might fail because the acme-dns configuration for one or more identifiers looks unhealthy.");
            }
            return ret;
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(AcmeOptions options)
        {
            yield return (Endpoint.Meta, options.BaseUri);
        }
    }
}
