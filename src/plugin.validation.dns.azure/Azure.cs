using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;
using Azure.ResourceManager.Resources;
using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Azure.Common;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("wacs.test")]

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    /// <summary>
    /// Handle creation of DNS records in Azure
    /// </summary>
    [IPlugin.Plugin1<
        AzureOptions, AzureOptionsFactory, 
        DnsValidationCapability, AzureJson, AzureArguments>
        ("aa57b028-45fb-4aca-9cac-a63d94c76b4a",
        "Azure", "Create verification records in Azure DNS")]
    internal class Azure(AzureOptions options,
        LookupClientProvider dnsClient,
        SecretServiceManager ssm,
        IProxyService proxyService,
        ILogService log,
        ISettingsService settings) : DnsValidation<Azure>(dnsClient, log, settings)
    {
        private ArmClient? _armClient;
        private SubscriptionResource? _subscriptionResource;
        private readonly AzureHelpers _helpers = new(options, proxyService, ssm);
        private readonly Dictionary<DnsZoneResource, Dictionary<string, DnsTxtRecordData>> _recordSets = [];
        private IEnumerable<DnsZoneResource>? _hostedZones;

        /// <summary>
        /// Allow this plugin to process multiple validations at the same time.
        /// They will still be prepared and cleaned in serial order though not
        /// to overwhelm the DnsManagementClient or risk threads overwriting 
        /// each others changes.
        /// </summary>
        public override ParallelOperations Parallelism => ParallelOperations.Answer;

        /// <summary>
        /// Mark DNS record for creation
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            var zone = await GetHostedZone(record.Authority.Domain);
            if (zone == null)
            {
                return false;
            }
            var relativeKey = RelativeRecordName(zone.Data.Name, record.Authority.Domain);
            if (!_recordSets.TryGetValue(zone, out var value))
            {
                value = [];
                _recordSets.Add(zone, value);
            }
            if (!value.ContainsKey(relativeKey))
            {
                try
                {
                    var existing = await zone.GetDnsTxtRecords().GetAsync(relativeKey);
                    value.Add(relativeKey, existing.Value.Data);
                } 
                catch
                {
                    value.Add(relativeKey, new DnsTxtRecordData() { TtlInSeconds = 60 });
                }
            }
            if (!value[relativeKey].DnsTxtRecords.Any(x => x.Values.Contains(record.Value)))
            {
                var txtRecord = new DnsTxtRecordInfo();
                txtRecord.Values.Add(record.Value);
                value[relativeKey].DnsTxtRecords.Add(txtRecord);
            }
            return true;
        }

        /// <summary>
        /// Mark DNS record for removal
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            var zone = await GetHostedZone(record.Authority.Domain);
            if (zone == null)
            {
                return;
            }
            var relativeKey = RelativeRecordName(zone.Data.Name, record.Authority.Domain);
            if (!_recordSets.TryGetValue(zone, out var recordSet))
            {
                return;
            }
            if (!recordSet.TryGetValue(relativeKey, out var txtResource))
            {
                return;
            }
            var removeList = txtResource.DnsTxtRecords.Where(x => x.Values.Contains(record.Value)).ToList();
            foreach (var remove in removeList)
            {
                _ = txtResource.DnsTxtRecords.Remove(remove);
            }
        }

        /// <summary>
        /// Send all buffered changes to Azure
        /// </summary>
        /// <returns></returns>
        public override async Task SaveChanges()
        {
            var updateTasks = new List<Task>();
            foreach (var zone in _recordSets.Keys)
            {
                foreach (var domain in _recordSets[zone].Keys)
                {
                    updateTasks.Add(PersistRecordSet(zone, domain, _recordSets[zone][domain]));
                }
            }
            await Task.WhenAll(updateTasks);
        }

        /// <summary>
        /// Store a single recordset
        /// </summary>
        /// <param name="zone"></param>
        /// <param name="domain"></param>
        /// <returns></returns>
        private async Task PersistRecordSet(DnsZoneResource zone, string domain, DnsTxtRecordData txtRecords)
        {
            try
            {
                var txtRecordCollection = zone.GetDnsTxtRecords();
                if (!txtRecords.DnsTxtRecords.Any())
                {
                    var existing = await txtRecordCollection.GetAsync(domain);
                    if (existing != null)
                    {
                        _ = await existing.Value.DeleteAsync(WaitUntil.Started);
                    }
                }
                else
                {
                    _ = await txtRecordCollection.CreateOrUpdateAsync(WaitUntil.Completed, domain, txtRecords);
                }
            } 
            catch (Exception ex)
            {
                _log.Error(ex, "Error updating DNS records in {zone}", zone.Data.Name);
            }
        }

        private ArmClient Client
        {
            get
            {
                _armClient ??= new ArmClient(
                        _helpers.TokenCredential,
                        options.SubscriptionId,
                        _helpers.ArmOptions);
                return _armClient;
            }
        }

        /// <summary>
        /// Get the subscription
        /// </summary>
        /// <returns></returns>
        private async Task<SubscriptionResource> Subscription()
        {
            _subscriptionResource ??= await Client.GetDefaultSubscriptionAsync();
            if (_subscriptionResource == null)
            {
                throw new Exception($"Unable to find subscription {options.SubscriptionId ?? "default"}");
            }
            return _subscriptionResource;
        }

        /// <summary>
        /// Find the appropriate hosting zone to use for record updates
        /// </summary>
        /// <param name="recordName"></param>
        /// <returns></returns>
        private async Task<DnsZoneResource?> GetHostedZone(string recordName)
        {
            var subscription = await Subscription();
          
            if (_hostedZones == null)
            {
                // Cache so we don't have to repeat this more than once for each renewal
                var cachedZones = new List<DnsZoneResource>();
                var zones = subscription.GetDnsZonesAsync();
                await foreach (var zone in zones)
                {
                    cachedZones.Add(zone);
                }
                _hostedZones = cachedZones;
            }

            // Option to bypass the best match finder
            if (!string.IsNullOrEmpty(options.HostedZone))
            {
                var match = _hostedZones.FirstOrDefault(h => string.Equals(h.Data.Name, options.HostedZone, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                {
                    _log.Error("Unable to find hosted zone {name}", options.HostedZone);
                }
                return match;
            }

            var hostedZone = FindBestMatch(_hostedZones.ToDictionary(x => x.Data.Name), recordName);
            if (hostedZone != null)
            {
                return hostedZone;
            }
            _log.Error(
                "Can't find hosted zone for {recordName} in subscription {subscription}",
                recordName,
                options.SubscriptionId);
            return null;
        }

        /// <summary>
        /// Clear created
        /// </summary>
        /// <returns></returns>
        public override async Task Finalize() =>
            // We save the original record sets, so this should restore them
            await SaveChanges();
    }
}
