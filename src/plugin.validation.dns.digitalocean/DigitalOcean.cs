﻿using DigitalOcean.API;
using DigitalOcean.API.Models.Requests;
using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [IPlugin.Plugin1<
        DigitalOceanOptions, DigitalOceanOptionsFactory,
        DnsValidationCapability, DigitalOceanJson, DigitalOceanArguments>
        ("1a87d670-3fa3-4a2a-bb10-491d48feb5db",
        "DigitalOcean", "Create verification records on DigitalOcean",
        External = true)]
    internal class DigitalOcean(
        DigitalOceanOptions options, LookupClientProvider dnsClient,
        SecretServiceManager ssm, ILogService log,
        ISettingsService settings) : DnsValidation<DigitalOcean>(dnsClient, log, settings)
    {
        private readonly IDigitalOceanClient _doClient = new DigitalOceanClient(ssm.EvaluateSecret(options.ApiToken));
        private long? _recordId;
        private string? _zone;

        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            try
            {
                if (_recordId == null)
                {
                    _log.Warning("Not deleting DNS records on DigitalOcean because of missing record id.");
                    return;
                }

                await _doClient.DomainRecords.Delete(_zone, _recordId.Value);
                _log.Information("Successfully deleted DNS record on DigitalOcean.");
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to delete DNS record on DigitalOcean.");
            }
        }

        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            try
            {
                var zones = await _doClient.Domains.GetAll();
                var zone = FindBestMatch(zones.Select(x => x.Name).ToDictionary(x => x), record.Authority.Domain);
                if (zone == null)
                {
                    _log.Error($"Unable to find a zone on DigitalOcean for '{record.Authority.Domain}'.");
                    return false;
                }

                var createdRecord = await _doClient.DomainRecords.Create(zone, new DomainRecord
                {
                    Type = "TXT",
                    Name = RelativeRecordName(zone, record.Authority.Domain),
                    Data = record.Value,
                    Ttl = 300
                });
                _recordId = createdRecord.Id;
                _zone = zone;
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Failed to create DNS record on DigitalOcean.");
                return false;
            }
        }
    }
}
