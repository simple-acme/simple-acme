using Newtonsoft.Json.Linq;
using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TencentCloud.Common;
using TencentCloud.Common.Profile;

//Api Key: http://console.cloud.tencent.com/cam/capi
//Api Doc: https://cloud.tencent.com/document/api/1427/56166
namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [IPlugin.Plugin1<TencentOptions, TencentOptionsFactory, DnsValidationCapability, TencentJson, TencentArguments>
        ("6ea628c3-0f74-68bb-cf17-4fdd3d53f3af",
        "Tencent", "Create verification records in Tencent DNS",
        Name = "Tencent Cloud", External = true)]
    public class Tencent(SecretServiceManager ssm,
        LookupClientProvider dnsClient, ILogService log, ISettings settings, IProxyService proxy,
        TencentOptions options) : DnsValidation<Tencent, CommonClient>(dnsClient, log, settings, proxy)
    {
        public override async Task<bool> CreateRecord(DnsValidationRecord record)
        {
            try
            {
                var identifier = await GetDomain(record) ?? throw new($"The domain name cannot be found: {record.Context.Identifier}");
                var domain = record.Authority.Domain;
                var value = record.Value;
                //Add Record
                return await AddRecord(identifier, domain, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                //Out Error
                _log.Error($"Unable to add TencentDNS record: {ex.Message}");
            }
            return false;
        }

        public override async Task DeleteRecord(DnsValidationRecord record)
        {
            try
            {
                var identifier = await GetDomain(record) ?? throw new($"The domain name cannot be found: {record.Context.Identifier}");
                var domain = record.Authority.Domain;
                //Delete Record
                _ = await DelRecord(identifier, domain);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                //Out Error
                _log.Error($"Unable to delete TencentDNS record: {ex.Message}");
            }
        }

        #region PrivateLogic

        /// <summary>
        /// Add Record
        /// </summary>
        /// <param name="domain">Domain</param>
        /// <param name="subDomain">SubDomain</param>
        /// <param name="value">Value</param>
        /// <returns></returns>
        private async Task<bool> AddRecord(string domain, string subDomain, string value)
        {
            subDomain = subDomain.Replace($".{domain}", "");
            //Delete Record
            _ = DelRecord(domain, subDomain);
            //Add Record
            var client = await GetClient();
            var param = new
            {
                Domain = domain,
                SubDomain = subDomain,
                RecordType = "TXT",
                RecordLine = "默认",
                Value = value,
            };
            var req = new CommonRequest(param);
            var act = "CreateRecord";
            client.Call(req, act);
            return true;
        }

        /// <summary>
        /// Delete Record
        /// </summary>
        /// <param name="domain">Domain</param>
        /// <param name="subDomain">SubDomain</param>
        /// <returns></returns>
        private async Task<bool> DelRecord(string domain, string subDomain)
        {
            subDomain = subDomain.Replace($".{domain}", "");
            //Get RecordID
            var recordId = GetRecordID(domain, subDomain);
            if (recordId == default) return false;
            //Delete Record
            var client = await GetClient();
            var param = new { Domain = domain, RecordId = recordId };
            var req = new CommonRequest(param);
            var act = "DeleteRecord";
            client.Call(req, act);
            return true;
        }

        /// <summary>
        /// Get RecordID
        /// </summary>
        /// <param name="domain">Domain</param>
        /// <param name="subDomain">SubDomain</param>
        /// <returns></returns>
        private async Task<long> GetRecordID(string domain, string subDomain)
        {
            var client = await GetClient();
            var param = new { Domain = domain };
            var req = new CommonRequest(param);
            var act = "DescribeRecordList";
            var resp = client.Call(req, act);
            //Console.WriteLine(resp);
            var json = JObject.Parse(resp);
            var jsonData = json["Response"]!["RecordList"];
            var jsonDataLinq = jsonData!.Where(w => w["Name"]!.ToString() == subDomain && w["Type"]!.ToString() == "TXT");
            if (jsonDataLinq.Any()) return (long)jsonDataLinq.First()["RecordId"]!;
            return default;
        }

        /// <summary>
        /// Get Domain
        /// </summary>
        /// <param name="record">DnsValidationRecord</param>
        /// <returns></returns>
        private async Task<string?> GetDomain(DnsValidationRecord record)
        {
            var client = await GetClient();
            var param = new { };
            var req = new CommonRequest(param);
            var act = "DescribeDomainList";
            var resp = client.Call(req, act);
            //Console.WriteLine(resp);
            var json = JObject.Parse(resp);
            var myDomains = json["Response"]!["DomainList"]!.Select(t => t["Name"]!.ToString());
            var zone = FindBestMatch(myDomains.ToDictionary(x => x), record.Authority.Domain);
            if (zone != null) return zone;
            return default;
        }

        /// <summary>
        /// DnsPod Server
        /// </summary>
        private const string DnsPodServer = "dnspod.tencentcloudapi.com";

        /// <summary>
        /// Get CommonClient
        /// </summary>
        /// <param name="modTemp">Mod</param>
        /// <param name="verTemp">Ver</param>
        /// <param name="regionTemp">Region</param>
        /// <param name="endpointTemp">DnsPodServer</param>
        /// <returns></returns>
        protected override async Task<CommonClient> CreateClient(HttpClient http)
        {
            var mod = "dnspod";
            var ver = "2021-03-23";
            var region = "";
            var hpf = new HttpProfile
            {
                ReqMethod = "POST",
                Endpoint = DnsPodServer,
            };
            var cpf = new ClientProfile(ClientProfile.SIGN_TC3SHA256, hpf);
            var cred = new Credential()
            {
                SecretId = await ssm.EvaluateSecret(options.ApiID),
                SecretKey = await ssm.EvaluateSecret(options.ApiKey)
            };
            return new CommonClient(mod, ver, cred, region, cpf);
        }

        #endregion PrivateLogic
    }
}
