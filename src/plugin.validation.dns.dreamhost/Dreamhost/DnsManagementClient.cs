﻿using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dreamhost
{
    public class DnsManagementClient(string apiKey, ILogService logService, HttpClient httpClient)
    {
        private readonly string uri = "https://api.dreamhost.com/";

        public async Task CreateRecord(string record, RecordType type, string value)
        {
            var response = await SendRequest("dns-add_record",
                new Dictionary<string, string>
                {
                    {"record", record},
                    {"type", type.ToString()},
                    {"value", value}
                });
            var content = await response.Content.ReadAsStringAsync();
            logService.Information("Dreamhost Responded with: {0}", content);
            logService.Information("Waiting for 30 seconds");
            await Task.Delay(30000);
        }

        public async Task DeleteRecord(string record, RecordType type, string value)
        {
            var args = new Dictionary<string, string>
            {
                {"record", record},
                {"type", type.ToString()},
                {"value", value}
            };
            var response = await SendRequest("dns-remove_record", args);
            var content = await response.Content.ReadAsStringAsync();
            logService.Information("Dreamhost Responded with: {0}", content);
            logService.Information("Waiting for 30 seconds");
            await Task.Delay(30000);
        }

        private async Task<HttpResponseMessage> SendRequest(string command, IEnumerable<KeyValuePair<string, string>> args)
        {
            httpClient.BaseAddress = new Uri(uri);
            var queryString = new Dictionary<string, string>
            {
                { "key", apiKey },
                { "unique_id", Guid.NewGuid().ToString() },
                { "format", "json" },
                { "cmd", command }
            };
            foreach (var arg in args)
            {
                queryString.Add(arg.Key, arg.Value);
            }
            return await httpClient.GetAsync("?" + string.Join("&", queryString.Select(kvp => $"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value)}")));
        }
    }
}