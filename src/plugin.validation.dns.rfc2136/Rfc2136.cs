using ARSoft.Tools.Net;
using ARSoft.Tools.Net.Dns;
using ARSoft.Tools.Net.Dns.DynamicUpdate;
using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ArDnsClient = ARSoft.Tools.Net.Dns.DnsClient;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [IPlugin.Plugin1<
        Rfc2136Options, Rfc2136OptionsFactory, 
        DnsValidationCapability, Rfc2136Json, Rfc2136Arguments>
        ("ed5dc9d1-739c-4f6a-854f-238bf65b63ee",
        "RFC2136",
        "Create verification records using dynamic updates",
        External = true)]
    internal sealed class Rfc2136(
        LookupClientProvider dnsClient,
        ILogService log,
        ISettingsService settings,
        SecretServiceManager ssm,
        LookupClientProvider lcp,
        DomainParseService domainParser,
        Rfc2136Options options) : DnsValidation<Rfc2136>(dnsClient, log, settings)
    {
        private readonly Dictionary<string, string> _zoneMap = [];
        private ArDnsClient? _client;

        /// <summary>
        /// Allow multiple validation at once when DisableMultiThreading = false
        /// </summary>
        public override ParallelOperations Parallelism => ParallelOperations.Answer | ParallelOperations.Reuse;

        /// <summary>
        /// Create record using AddRecordUpdate message
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            var domain = record.Authority.Domain;
            var topZone = _zoneMap.TryGetValue(domain, out string? value) ? value : domainParser.GetRegisterableDomain(domain);
            var subDomains = domain.
                Split(".").
                SkipLast(topZone.Split(".").Length).
                ToList();

            var currentZone = topZone;
            while (true)
            {
                var msg = new DnsUpdateMessage { ZoneName = DomainName.Parse(currentZone) };
                msg.Updates.Add(
                    new AddRecordUpdate(
                        new TxtRecord(
                            DomainName.Parse(domain),
                            60,
                            record.Value)));
                try
                {
                    _log.Verbose("Attempt to create {domain} in zone {zone}", domain, currentZone);
                    await SendUpdate(msg);

                    // Cache succesful zone result after succesful
                    // update so that we don't have to retry again
                    // for subsequent adds and deletes.
                    _zoneMap.TryAdd(domain, currentZone);
                    return true;
                }
                catch (Exception ex)
                {
                    _log.Debug("Error creating {domain} in zone {zone}: {ex}", domain, currentZone, ex.Message);
                }

                if (subDomains.Count != 0)
                {
                    currentZone = $"{subDomains.Last()}.{currentZone}";
                    subDomains.RemoveAt(subDomains.Count - 1);
                }
                else
                {
                    return false;
                }
            }       
        }

        /// <summary>
        /// Delete record using DeleteRecordUpdate
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            var domain = record.Authority.Domain;
            var topZone = _zoneMap.TryGetValue(domain, out string? value) ? value : domainParser.GetRegisterableDomain(domain);
            var msg = new DnsUpdateMessage { ZoneName = DomainName.Parse(topZone) };
            var txtRecord = new TxtRecord(DomainName.Parse(domain), 0, record.Value);
            var delete = new DeleteRecordUpdate(txtRecord);
            msg.Updates.Add(delete);
            try
            {
                _log.Verbose("Deleting record {name} in zone {zone}", record.Authority.Domain, topZone);
                await SendUpdate(msg);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error deleting DNS record");
            }
        }

        /// <summary>
        /// Construct client and cache result
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="Exception"></exception>
        private async Task<ArDnsClient> GetClient()
        {
            if (_client == null)
            {
                var port = options.ServerPort ?? 53;
                if (!IPAddress.TryParse(options.ServerHost, out var ipAddress))
                {
                    if (string.IsNullOrEmpty(options.ServerHost))
                    {
                        throw new InvalidOperationException("Missing ServerHost");
                    }
                    var lookup = await lcp.
                        GetSystemClient().
                        GetIps(options.ServerHost);
                    if (!lookup.Any())
                    {
                        throw new Exception($"Unable to find IP for {options.ServerHost}");
                    }
                    ipAddress = lookup.First();
                }
                _log.Verbose("Connnecting to DNS server at {ipAddress}:{port} using key {key}", ipAddress, port, options.TsigKeyName);
                _client = new ArDnsClient(ipAddress, port);
            }
            return _client;
        }

        /// <summary>
        /// Add TSIG options and send the message to the server
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private async Task SendUpdate(DnsUpdateMessage msg)
        {
            if (!Enum.TryParse<TSigAlgorithm>(options.TsigKeyAlgorithm, true, out var algorithm)) 
            {
                algorithm = TSigAlgorithm.Md5;
            }

            var key = await ssm.EvaluateSecret(options.TsigKeySecret);
            if (string.IsNullOrEmpty(key))
            {
                throw new InvalidOperationException("Missing TsigKeySecret");
            }

            msg.TSigOptions = new TSigRecord(
                DomainName.Parse(options.TsigKeyName ?? ""),
                algorithm,
                DateTime.Now,
                new TimeSpan(0, 5, 0),
                msg.TransactionID,
                ReturnCode.NoError,
                null,
                Convert.FromBase64String(key));

            var client = await GetClient();
            _log.Verbose("Send DNS update transaction {TransactionID}");
            var ret = await client.SendUpdateAsync(msg);
            if (ret == null || ret.ReturnCode != ReturnCode.NoError)
            {
                throw new Exception(ret?.ReturnCode.ToString() ?? "no response");
            }
        }
    }
}