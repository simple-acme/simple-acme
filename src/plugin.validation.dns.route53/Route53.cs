using Amazon;
using Amazon.Route53;
using Amazon.Route53.Model;
using Amazon.Runtime;
using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [IPlugin.Plugin1<
        Route53Options, Route53OptionsFactory, 
        DnsValidationCapability, Route53Json, Route53Arguments>
        ("4e5dc595-45c7-4461-929a-8f96a0c96b3d", 
        "Route53", "Create verification records in Route 53 DNS", 
        Name = "Route 53", External = true, Provider = "Amazon AWS")]
    internal sealed class Route53(
        LookupClientProvider dnsClient,
        ILogService log,
        IProxyService proxy,
        ISettings settings,
        SecretServiceManager ssm,
        Route53Options options) : DnsValidation<Route53, AmazonRoute53Client>(dnsClient, log, settings, proxy)
    {
        internal const string DefaultRegion = "us-east-1";

        private readonly Dictionary<string, List<ResourceRecordSet>> _pendingZoneUpdates = [];
        public override ParallelOperations Parallelism => ParallelOperations.Answer;

        private async Task<AWSCredentials?> GetCredentials()
        {
            var baseCredential = default(AWSCredentials);
            var proxy = await _proxy.GetWebProxy();
            if (!string.IsNullOrWhiteSpace(options.IAMRole))
            {
                baseCredential = new InstanceProfileAWSCredentials(options.IAMRole, proxy);
            }
            if (!string.IsNullOrWhiteSpace(options.AccessKeyId))
            {
                var accessKey = await ssm.EvaluateSecret(options.SecretAccessKey);
                baseCredential = new BasicAWSCredentials(options.AccessKeyId, accessKey);
            }
            baseCredential ??= new InstanceProfileAWSCredentials(proxy);
            if (!string.IsNullOrWhiteSpace(options.ARNRole))
            {
                baseCredential = new AssumeRoleAWSCredentials(
                    baseCredential, 
                    options.ARNRole, 
                    _settings.Client.ClientName, 
                    new AssumeRoleAWSCredentialsOptions() { ProxySettings = proxy });
            }
            return baseCredential;
        }

        protected override async Task<AmazonRoute53Client> CreateClient(HttpClient httpClient)
        {
            var credential = await GetCredentials();
            var regionName = string.IsNullOrWhiteSpace(options.Region) ? DefaultRegion : options.Region;
            var region = RegionEndpoint.GetBySystemName(regionName); 
            _log.Information("Using AWS region {region}", region.DisplayName);
            var config = new AmazonRoute53Config() { RegionEndpoint = region };
            config.SetWebProxy(await _proxy.GetWebProxy());
            return new AmazonRoute53Client(credential, config);
        }

        private void CreateOrUpdateResourceRecordSet(string hostedZone, string name, string record)
        {
            lock (_pendingZoneUpdates)
            {
                if (!_pendingZoneUpdates.TryGetValue(hostedZone, out List<ResourceRecordSet>? value))
                {
                    value = [];
                    _pendingZoneUpdates.Add(hostedZone, value);
                }
                var pendingRecordSets = value;
                var existing = pendingRecordSets.FirstOrDefault(x => x.Name == name);
                if (existing == null)
                {
                    existing = new ResourceRecordSet
                    {
                        Name = name,
                        Type = RRType.TXT,
                        ResourceRecords = [],
                        TTL = 1L
                    };
                    pendingRecordSets.Add(existing);
                }
                var formattedValue = $"\"{record}\"";
                if (!existing.ResourceRecords.Any(x => x.Value == formattedValue))
                {
                    existing.ResourceRecords.Add(new ResourceRecord(formattedValue));
                }
            }
        }

        /// <summary>
        /// Only create a list of which sets we are going to create in each zone.
        /// Changes are only submitted in the SaveChanges phase.
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            try
            {
                var recordName = record.Authority.Domain;
                var token = record.Value;
                var hostedZoneIds = await GetHostedZoneIds(recordName);
                if (hostedZoneIds == null)
                {
                    return false;
                }
                _log.Information("Creating TXT record {recordName} with value {token}", recordName, token);
                foreach (var zone in hostedZoneIds)
                {
                    CreateOrUpdateResourceRecordSet(zone, recordName, token);
                }
                return true;
            }
            catch (Exception ex)
            {
                _log.Warning(ex, $"Error creating TXT record");
                return false;
            }
        }

        /// <summary>
        /// Start pending zone updates
        /// </summary>
        /// <returns></returns>
        public override async Task SaveChanges()
        {
            var client = await GetClient();
            var updateTasks = new List<Task<ChangeResourceRecordSetsResponse>>();
            foreach (var zone in _pendingZoneUpdates.Keys)
            {
                var recordSets = _pendingZoneUpdates[zone];
                updateTasks.Add(client.ChangeResourceRecordSetsAsync(
                    new ChangeResourceRecordSetsRequest(
                        zone,
                        new ChangeBatch(recordSets.Select(x => new Change(ChangeAction.UPSERT, x)).ToList()))));
            }

            var results = await Task.WhenAll(updateTasks);
            var pendingChanges = results.Select(result => result.ChangeInfo);
            var propagationTasks = pendingChanges.Select(WaitChangesPropagation);
            await Task.WhenAll(propagationTasks);
        }

        /// <summary>
        /// Delete created records, do not wait for propagation here
        /// </summary>
        /// <returns></returns>
        public override async Task Finalize()
        {
            var client = await GetClient();
            var deleteTasks = new List<Task<ChangeResourceRecordSetsResponse>>();
            foreach (var zone in _pendingZoneUpdates.Keys)
            {
                var recordSets = _pendingZoneUpdates[zone];
                deleteTasks.Add(client.ChangeResourceRecordSetsAsync(
                    new ChangeResourceRecordSetsRequest(
                        zone,
                        new ChangeBatch(recordSets.Select(x => new Change(ChangeAction.DELETE, x)).ToList()))));
            }
            _ = await Task.WhenAll(deleteTasks);
        }      

        /// <summary>
        /// Find matching hosted zones
        /// </summary>
        /// <param name="recordName"></param>
        /// <returns></returns>
        private async Task<IEnumerable<string>?> GetHostedZoneIds(string recordName)
        {
            var client = await GetClient();
            var hostedZones = new List<HostedZone>();
            var response = await client.ListHostedZonesAsync();
            hostedZones.AddRange(response.HostedZones);
            while (response.IsTruncated == true)
            {
                response = await client.ListHostedZonesAsync(
                    new ListHostedZonesRequest() {
                        Marker = response.NextMarker
                    });
                hostedZones.AddRange(response.HostedZones);
            }
            _log.Debug("Found {count} hosted zones in AWS", hostedZones.Count);

            hostedZones = [.. hostedZones.Where(x => !x.Config.PrivateZone == true)];
            var hostedZoneSets = hostedZones.GroupBy(x => x.Name);
            var hostedZone = FindBestMatch(hostedZoneSets.ToDictionary(x => x.Key), recordName);
            if (hostedZone != null)
            {
                return hostedZone.Select(x => x.Id);
            }
            _log.Error($"Can't find hosted zone for domain {recordName}");
            return null;
        }

        /// <summary>
        /// Wait for changes to propagate
        /// </summary>
        /// <param name="changeInfo"></param>
        /// <returns></returns>
        private async Task WaitChangesPropagation(ChangeInfo changeInfo)
        {
            var client = await GetClient();
            if (changeInfo.Status == ChangeStatus.INSYNC)
            {
                return;
            }
            _log.Information("Waiting for DNS changes propagation");
            var changeRequest = new GetChangeRequest(changeInfo.Id);
            while ((await client.GetChangeAsync(changeRequest)).ChangeInfo.Status == ChangeStatus.PENDING)
            {
                await Task.Delay(2000);
            }
        }
    }
}