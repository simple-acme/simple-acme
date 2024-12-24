using PKISharp.WACS.Clients;
using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
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
    internal partial class Script(
        ScriptOptions options,
        LookupClientProvider dnsClient,
        ScriptClient client,
        ILogService log,
        DomainParseService domainParseService,
        SecretServiceManager secretServiceManager,
        ISettingsService settings) : DnsValidation<Script>(dnsClient, log, settings)
    {
        internal const string DefaultCreateArguments = "create {Identifier} {RecordName} {Token}";
        internal const string DefaultDeleteArguments = "delete {Identifier} {RecordName} {Token}";

        public override ParallelOperations Parallelism => (ParallelOperations)(options.Parallelism ?? 0);

        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            var script = options.Script ?? options.CreateScript;
            if (!string.IsNullOrWhiteSpace(script))
            {
                var args = DefaultCreateArguments;
                if (!string.IsNullOrWhiteSpace(options.CreateScriptArguments))
                {
                    args = options.CreateScriptArguments;
                }
                var escapeToken = script.EndsWith(".ps1");
                var actualArguments = ProcessArguments(record.Context.Identifier, record.Authority.Domain, record.Value, args, escapeToken, false);
                var censoredArguments = ProcessArguments(record.Context.Identifier, record.Authority.Domain, record.Value, args, escapeToken, true);
                return await client.RunScript(
                    script,
                    actualArguments,
                    censoredArguments);
            }
            else
            {
                _log.Error("No create script configured");
                return false;
            }
        }

        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            var script = options.Script ?? options.DeleteScript;
            if (!string.IsNullOrWhiteSpace(script))
            {
                var args = DefaultDeleteArguments;
                if (!string.IsNullOrWhiteSpace(options.DeleteScriptArguments))
                {
                    args = options.DeleteScriptArguments;
                }
                var escapeToken = script.EndsWith(".ps1");
                var actualArguments = ProcessArguments(record.Context.Identifier, record.Authority.Domain, record.Value, args, escapeToken, false);
                var censoredArguments = ProcessArguments(record.Context.Identifier, record.Authority.Domain, record.Value, args, escapeToken, true);
                await client.RunScript(script, actualArguments, censoredArguments);
            }
            else
            {
                _log.Warning("No delete script configured, validation record remains");
            }
        }

        private string ProcessArguments(string identifier, string recordName, string token, string args, bool escapeToken, bool censor)
        {
            var ret = args;
            // recordName: _acme-challenge.sub.domain.com
            // zoneName: domain.com
            // nodeName: _acme-challenge.sub

            // recordName: domain.com
            // zoneName: domain.com
            // nodeName: @

            var zoneName = domainParseService.GetRegisterableDomain(identifier);
            var nodeName = "@";
            if (recordName.Length > zoneName.Length)
            {
                // Offset by one to prevent trailing dot
                var idx = recordName.Length - zoneName.Length - 1;
                if (idx != 0)
                {
                    nodeName = recordName[..idx];
                }
            }

            // Some tokens start with - which confuses Powershell. We did not want to 
            // make a breaking change for .bat or .exe files, so instead escape the 
            // token with double quotes, as Powershell discards the quotes anyway and 
            // thus it's functionally equivalant.
            if (escapeToken && (ret.Contains(" {Token} ") || ret.EndsWith(" {Token}")))
            {
                ret = ret.Replace("{Token}", "\"{Token}\"");
            }

            // Numbered parameters for backwards compatibility only,
            // do not extend for future updates
            return TokenRegex().Replace(ret, (m) => {
                return m.Value switch
                {
                    "{ZoneName}" => zoneName,
                    "{NodeName}" => nodeName,
                    "{Identifier}" => identifier,
                    "{RecordName}" => recordName,
                    "{Token}" => token,
                    var s when s.StartsWith($"{{{SecretServiceManager.VaultPrefix}") =>
                        censor ? s : secretServiceManager.EvaluateSecret(s.Trim('{', '}')) ?? s,
                    _ => m.Value
                };
            });
        }

        [GeneratedRegex("{.+?}")]
        private static partial Regex TokenRegex();
    }
}
