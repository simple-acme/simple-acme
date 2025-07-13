using PKISharp.WACS.Clients;
using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Any
{
    internal partial class ScriptDns(
        ScriptOptions options,
        LookupClientProvider dnsClient,
        ScriptClient client,
        ILogService log,
        SecretServiceManager secretServiceManager,
        DomainParseService domainParseService,
        ISettings settings) : DnsValidation<ScriptDns>(dnsClient, log, settings)
    {

        public override ParallelOperations Parallelism => (ParallelOperations)(options.Parallelism ?? 0);

        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            var script = options.Script ?? options.CreateScript;
            if (!string.IsNullOrWhiteSpace(script))
            {
                var args = Script.DefaultCreateArguments;
                if (!string.IsNullOrWhiteSpace(options.CreateScriptArguments))
                {
                    args = options.CreateScriptArguments;
                }
                var escapeToken = script.EndsWith(".ps1");
                var actualArguments = await ProcessArguments(record.Context.Identifier, record.Authority.Domain, record.Value, args, escapeToken, false);
                var censoredArguments = await ProcessArguments(record.Context.Identifier, record.Authority.Domain, record.Value, args, escapeToken, true);
                var result = await client.RunScript(script, actualArguments, censoredArguments);
                return result.Success;
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
                var args = Script.DefaultDeleteArguments;
                if (!string.IsNullOrWhiteSpace(options.DeleteScriptArguments))
                {
                    args = options.DeleteScriptArguments;
                }
                var escapeToken = script.EndsWith(".ps1");
                var actualArguments =  await ProcessArguments(record.Context.Identifier, record.Authority.Domain, record.Value, args, escapeToken, false);
                var censoredArguments = await ProcessArguments(record.Context.Identifier, record.Authority.Domain, record.Value, args, escapeToken, true);
                await client.RunScript(script, actualArguments, censoredArguments);
            }
            else
            {
                _log.Warning("No delete script configured, validation record remains");
            }
        }

        private async Task<string> ProcessArguments(string identifier, string recordName, string token, string args, bool escapeToken, bool censor)
        {
            var ret = args;
            var zoneName = domainParseService.GetRegisterableDomain(identifier);
            var nodeName = RelativeRecordName(zoneName, recordName);
           
            // Some tokens start with - which confuses Powershell. We did not want to 
            // make a breaking change for .bat or .exe files, so instead escape the 
            // token with double quotes, as Powershell discards the quotes anyway and 
            // thus it's functionally equivalant.
            if (escapeToken && (ret.Contains(" {Token} ") || ret.EndsWith(" {Token}")))
            {
                ret = ret.Replace("{Token}", "\"{Token}\"");
            }

            // Replace tokens in the script
            var replacements = new Dictionary<string, string?>
            {
                { "ZoneName", zoneName },
                { "NodeName", nodeName },
                { "Identifier", identifier },
                { "RecordName", recordName },
                { "Token", censor ? "***" : token }
            };
            return await ScriptClient.ReplaceTokens(ret, replacements, secretServiceManager, censor);
        }
    }
}
