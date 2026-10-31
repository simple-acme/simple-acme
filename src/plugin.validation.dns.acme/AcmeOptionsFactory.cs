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
        SecretServiceManager secretService,
        ArgumentsInputService arguments) : PluginOptionsFactory<AcmeOptions>
    {
        private ArgumentResult<string?> Endpoint => arguments.
            GetString<AcmeArguments>(x => x.AcmeDnsServer).
            Validate(x => Task.FromResult(new Uri(x!).ToString() != ""), "invalid uri").
            Required();

        private async Task<NetworkCredentialOptions?> Credentials(Uri uri, IInputService? input = null)
        {
            var credential = await NetworkCredentialOptions.Create<AcmeArguments>(arguments);
            if (string.IsNullOrEmpty(credential.UserName) && credential.Password == null)
            {
                if (input == null || !string.IsNullOrEmpty(uri.UserInfo) ||
                    !await input.PromptYesNo("Use Basic Authentication for the acme-dns server?", false))
                {
                    return null;
                }
                credential = await NetworkCredentialOptions.Create<AcmeArguments>(arguments, input, "acme-dns server");
            }
            if (string.IsNullOrWhiteSpace(credential.UserName))
            {
                throw new ArgumentException("Basic Authentication requires a username (--username).");
            }
            if (credential.UserName.Contains(':'))
            {
                throw new ArgumentException("Basic Authentication usernames cannot contain ':'.");
            }
            return credential;
        }

        public override async Task<AcmeOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            var ret = new AcmeOptions()
            {
                BaseUri = await Endpoint.Interactive(input).GetValue()
            };
            var uri = new Uri(ret.BaseUri!);
            ret.Credential = await Credentials(uri, input);
            var acmeDnsClient = new AcmeDnsClient(dnsClient, proxy, log, settings, input, uri, ret.Credential, secretService);
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
            var uri = new Uri(ret.BaseUri!);
            ret.Credential = await Credentials(uri);
            var acmeDnsClient = new AcmeDnsClient(dnsClient, proxy, log, settings, null, uri, ret.Credential, secretService);
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
            if (options.Credential != null)
            {
                foreach (var item in options.Credential.Describe<AcmeArguments>(arguments))
                {
                    yield return item;
                }
            }
        }
    }
}
