﻿using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PKISharp.WACS.Clients.Acme
{
    /// <summary>
    /// Helper class to support the https://zerossl.com/ API
    /// </summary>
    internal class ZeroSsl(IProxyService proxy, ILogService log)
    {
        /// <summary>
        /// Register new account using email address
        /// </summary>
        /// <param name="email"></param>
        /// <returns></returns>
        public async Task<ZeroSslEabCredential?> Register(string email)
        {
            var formContent = new FormUrlEncodedContent(
            [
                new KeyValuePair<string?, string?>("email", email)
            ]);
            try
            {
                var httpClient = await proxy.GetHttpClient();
                var response = await httpClient.PostAsync("https://api.zerossl.com/acme/eab-credentials-email", formContent);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize(content, WacsJson.Insensitive.ZeroSslEabCredential);
                    if (result == null)
                    {
                        log.Error("Unexpected response while attemting to register at ZeroSsl");
                    }
                    else if (result.Success == true)
                    {
                        return result;
                    }
                    else if (result.Error != null)
                    {
                        log.Error("Error attemting to register at ZeroSsl: {type} ({code})", result.Error.Type, result.Error.Code);
                    }
                    else 
                    {
                        log.Error("Invalid response while attemting to register at ZeroSsl");
                    }
                }
                else
                {
                    log.Error("Unexpected response while attemting to register at ZeroSsl");
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "Unexpected error while attemting to register at ZeroSsl");
            }
            return null;
        }

        /// <summary>
        /// Obtain EAB credential for account using API access key
        /// </summary>
        /// <param name="accessKey"></param>
        /// <returns></returns>
        public async Task<ZeroSslEabCredential?> Obtain(string accessKey)
        {
            try
            {
                var httpClient = await proxy.GetHttpClient();
                var response = await httpClient.PostAsync($"https://api.zerossl.com/acme/eab-credentials?access_key={accessKey}", new StringContent(""));
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize(content, WacsJson.Insensitive.ZeroSslEabCredential);
                    if (result == null)
                    {
                        log.Error("Invalid response while attemting to obtain credential from ZeroSsl");
                    }
                    else if (result.Success == true)
                    {
                        return result;
                    }
                    else if (result.Error != null)
                    {
                        log.Error("Error attemting to obtain credential from ZeroSsl: {type} ({code})", result.Error.Type, result.Error.Code);
                    }
                    else
                    {
                        log.Error("Invalid response while attemting to obtain credential from ZeroSsl");
                    }
                }
                else
                {
                    log.Error("Unexpected response while attemting to obtain credential from ZeroSsl");
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "Unexpected error while attemting to obtain credential from ZeroSsl");
            }

            return null;
        }

        /// <summary>
        /// Returned EAB credentials
        /// </summary>
        public class ZeroSslEabCredential
        {
            [JsonPropertyName("success")]
            public bool? Success { get; set; }

            [JsonPropertyName("error")]
            public ZeroSslApiError? Error { get; set; }

            [JsonPropertyName("eab_kid")]
            public string? Kid { get; set; }

            [JsonPropertyName("eab_hmac_key")]
            public string? Hmac { get; set; }
        }

        /// <summary>
        /// Error message
        /// </summary>
        public class ZeroSslApiError
        {
            [JsonPropertyName("code")]
            public int? Code { get; set; }

            [JsonPropertyName("type")]
            public string? Type { get; set; }
        }

    }
}
