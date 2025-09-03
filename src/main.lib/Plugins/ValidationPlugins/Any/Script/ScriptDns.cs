using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Any
{
    internal partial class ScriptDns(
        Script parent,
        LookupClientProvider dnsClient,
        DomainParseService domainParseService,
        ILogService log,
        ISettings settings) : DnsValidation<ScriptDns>(dnsClient, log, settings)
    {
        internal const string DefaultPrepareArguments = "create {Identifier} {RecordName} {Token}";
        internal const string DefaultCleanupArguments = "delete {Identifier} {RecordName} {Token}";

        public override async Task<bool> CreateRecord(DnsValidationRecord record) => 
            await parent.Create(record.Context.Identifier, record.Authority.Domain, record.Value);

        public override async Task DeleteRecord(DnsValidationRecord record) =>
            await parent.Delete(record.Context.Identifier, record.Authority.Domain, record.Value);

        internal Dictionary<string, string?> ReplaceTokens(string identifier, string recordName, bool censor, string token)
        {
            var zoneName = domainParseService.GetRegisterableDomain(identifier);
            var nodeName = DnsValidation<Script>.RelativeRecordName(zoneName, recordName);
            return new Dictionary<string, string?>
                {
                    { "ZoneName", zoneName },
                    { "NodeName", nodeName },
                    { "Identifier", identifier },
                    { "RecordName", recordName },
                    { "Token", censor ? "***" : token }
                };
        }

        internal static void ExplainReplacements(IInputService input)
        {
            input.Show("{Identifier}", "Identifier that is being validated, e.g. sub.example.com");
            input.Show("{RecordName}", "Full TXT record, e.g. _acme-challenge.sub.example.com");
            input.Show("{ZoneName}", "Registerable part, e.g. example.com");
            input.Show("{NodeName}", "Subdomain, e.g. _acme-challenge.sub");
            input.Show("{Token}", "Contents of the TXT record");
            input.Show("{vault://json/key}", "Secret from the vault");
        }
    }
}
