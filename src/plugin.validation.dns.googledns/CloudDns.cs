using Google.Apis.Auth.OAuth2;
using Google.Apis.Dns.v1;
using Google.Apis.Dns.v1.Data;
using Google.Apis.Http;
using Google.Apis.Services;
using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [IPlugin.Plugin1<
        CloudDnsOptions, CloudDnsOptionsFactory, 
        DnsValidationCapability, CloudDnsJson, CloudDnsArguments>
        ("B61505E9-1709-43FD-996F-C74C3686286C",
        "GCPDns", "Create verification records in Google Cloud DNS", 
        Name = "Cloud DNS", Provider = "Google", Download = "googledns", External = true, Page = "clouddns")]
    internal class CloudDns(
        LookupClientProvider dnsClient,
        ILogService log,
        IProxyService proxy,
        ISettings settings,
        CloudDnsOptions options) : DnsValidation<CloudDns, CloudDnsService>(dnsClient, log, settings, proxy)
    {
        private readonly CloudDnsOptions _options = options;

        protected override async Task<CloudDnsService> CreateClient(HttpClient httpClient)
        {
            GoogleCredential credential;
            if (!_options.ServiceAccountKeyPath.ValidFile(_log))
            {
                throw new Exception("Configuration error");
            }
            using (var stream = new FileStream(_options.ServiceAccountKeyPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream);
            }
            var handler = await _proxy.GetHttpMessageHandler();
            var dnsService = new DnsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                HttpClientFactory = new ProxyFactory(handler),
                ApplicationName = $"simple-acme {VersionService.SoftwareVersion}",
            });
            return new CloudDnsService(dnsService);
        }

        private class ProxyFactory(HttpMessageHandler handler) : HttpClientFactory
        {
            protected override HttpMessageHandler CreateHandler(CreateHttpClientArgs args)
            {
                return handler;
            }
        }

        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            var client = await GetClient();
            var recordName = record.Authority.Domain;
            var token = record.Value;
            _log.Information("Creating TXT record {recordName} with value {token}", recordName, token);

            var zone = await GetManagedZone(_options.ProjectId, recordName);
            if (zone == null)
            {
                _log.Error("The zone could not be found in Google Cloud DNS.  DNS validation record not created");
                return false;
            }

            try
            {
                _ = await client.CreateTxtRecord(_options.ProjectId ?? "", zone, recordName, token);
                return true;
            }
            catch(Exception ex)
            {
                _log.Warning(ex, "Error creating TXT record");
                return false;
            }
        }

        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            var client = await GetClient();
            var recordName = record.Authority.Domain;
            var zone = await GetManagedZone(_options.ProjectId, recordName);
            if (zone == null)
            {
                _log.Warning("Could not find zone '{0}' in project '{1}'", recordName, _options.ProjectId);
                return;
            }

            try
            {
                _ = await client.DeleteTxtRecord(_options.ProjectId ?? "", zone, recordName);
                _log.Debug("Deleted TXT record");
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error deleting TXT record");
                return;
            }
        }

        private async Task<ManagedZone?> GetManagedZone(string? projectId, string recordName)
        {
            var client = await GetClient();
            var hostedZones = await client.GetManagedZones(projectId ?? "");
            _log.Debug("Found {count} hosted zones in Google DNS", hostedZones.Count);

            var hostedZoneSets = hostedZones.Where(x => x.Visibility == "public").GroupBy(x => x.DnsName);
            var hostedZone = FindBestMatch(hostedZoneSets.ToDictionary(x => x.Key), recordName);
            if (hostedZone != null)
            {
                return hostedZone.First();
            }
            _log.Error($"Can't find hosted zone for domain {recordName}");
            return null;
        }
    }
}
