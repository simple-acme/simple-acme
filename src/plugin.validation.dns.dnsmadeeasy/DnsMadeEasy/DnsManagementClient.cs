﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.DnsMadeEasy
{
    public class DnsManagementClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private const string _uri = "https://api.dnsmadeeasy.com/";

        #region Lookup Domain Id
        public async Task<string> LookupDomainId(string domain)
        {
            var buildApiUrl = $"V2.0/dns/managed/name?domainname={domain}";

            var response = await _httpClient.GetAsync(buildApiUrl);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                string json = await response.Content.ReadAsStringAsync();
                var request = JsonConvert.DeserializeObject<DomainResponse>(json);

                if (request == null || request.Id == null)
                    throw new ArgumentNullException($"Unexpected null response for {domain}");

                if (!string.Equals(request.Name, domain, StringComparison.InvariantCultureIgnoreCase))
                    throw new InvalidDataException($"Domain returned an unexpected result requested: {domain} != {request.Name}");

                return request.Id;
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new Exception(content);
            }
        }

        private class DomainResponse
        {
            public string? Id { get; set; }
            public string? Name { get; set; }
            public string? Type { get; set; }
        }
        #endregion

        #region Lookup Domain Record Id
        public async Task<string[]> LookupDomainRecordId(string domainId, string recordName, RecordType type)
        {
            string recordType = type.ToString();
            var buildApiUrl = $"V2.0/dns/managed/{domainId}/records?recordName={recordName}&type={recordType}";

            var response = await _httpClient.GetAsync(buildApiUrl);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                string json = await response.Content.ReadAsStringAsync();
                var request = JsonConvert.DeserializeObject<DomainResponseCollection>(json);

                if (request == null || request.Data == null || request.Data.Length == 0)
                    return [];

                List<string> recordId = [];
                foreach (var result in request.Data)
                {
                    if (string.Equals(result.Name, recordName, StringComparison.InvariantCultureIgnoreCase) &&
                        string.Equals(result.Type, recordType, StringComparison.InvariantCultureIgnoreCase) &&
                        result.Id != null)
                    {
                        recordId.Add(result.Id);
                    }
                }

                return [.. recordId];
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new Exception(content);
            }
        }

        private class DomainResponseCollection
        {
            public DomainRequest[]? Data { get; set; }
        }

        private class DomainRequest : DomainResponse {}
        #endregion

        public DnsManagementClient(string apiKey, string apiSecret, HttpClient client)
        {
            client.BaseAddress = new Uri(_uri);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var currentDate = DateTime.UtcNow.ToString("r");
            client.DefaultRequestHeaders.Add("x-dnsme-apiKey", apiKey);
            client.DefaultRequestHeaders.Add("x-dnsme-requestDate", currentDate);
            client.DefaultRequestHeaders.Add("x-dnsme-hmac", HMACSHA1(currentDate, apiSecret));
            _httpClient = client;
        }

        private static string HMACSHA1(string text, string key)
        {
            using var hmacsha256 = new HMACSHA1(Encoding.UTF8.GetBytes(key));
            var hash = hmacsha256.ComputeHash(Encoding.UTF8.GetBytes(text));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        public async Task CreateRecord(string domain, string recordName, RecordType type, string value)
        {
            string domainId = await LookupDomainId(domain);

            // Ensure any existing record are deleted.
            string[] domainRecordIds = await LookupDomainRecordId(domainId, recordName, type);
            if (domainRecordIds.Length > 0)
            {
                await DeleteRecord(domainId, domainRecordIds);
            }

 
            var putData = new { name = recordName, type = type.ToString(), value, ttl = 600, gtdLocation = "DEFAULT" };
            var serializedObject = JsonConvert.SerializeObject(putData);

            //Record successfully created
            // Wrap our JSON inside a StringContent which then can be used by the HttpClient class
            var httpContent = new StringContent(serializedObject, Encoding.UTF8, "application/json");
            var buildApiUrl = $"V2.0/dns/managed/{domainId}/records/";

            var response = await _httpClient.PostAsync(buildApiUrl, httpContent);
            if (response.StatusCode == HttpStatusCode.Created)
            {
                var content = await response.Content.ReadAsStringAsync();
                //_logService.Information("DnsMadeEasy Created Responded with: {0}", content);
                //_logService.Information("Waiting for 30 seconds");
                //await Task.Delay(30000);
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                throw new Exception(content);
            }
        }
        public async Task DeleteRecord(string domain, string recordName, RecordType type)
        {
            string domainId = await LookupDomainId(domain);
            string[] domainRecordIds = await LookupDomainRecordId(domainId, recordName, type);

            await DeleteRecord(domainId, domainRecordIds);
        }
        public async Task DeleteRecord(string domainId, string[] domainRecordIds)
        {
            foreach (var domainRecordId in domainRecordIds)
            {
                var buildApiUrl = $"V2.0/dns/managed/{domainId}/records/{domainRecordId}";

                var response = await _httpClient.DeleteAsync(buildApiUrl);
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var content = await response.Content.ReadAsStringAsync();
                }
                else
                {
                    var content = await response.Content.ReadAsStringAsync();
                    throw new Exception(content);
                }
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}