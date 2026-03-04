using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [IPlugin.Plugin1<
    CzechiaOptions, CzechiaOptionsFactory,
    DnsValidationCapability, CzechiaJson, CzechiaArguments>
    ("837692a9-25bc-4adc-908e-dc3316f11e32",
     "Czechia", "Create verification records in Czechia DNS",
     External = false)]
     public class Czechia(
         CzechiaOptions options,
         IProxyService proxyService,
         LookupClientProvider dnsClient,
         SecretServiceManager ssm,
         ILogService log,
         ISettings settings) : DnsValidation<Czechia, HttpClient>(dnsClient, log, settings, proxyService)
         {
             protected override async Task<HttpClient> CreateClient(HttpClient client)
             {
                 var token = await ssm.EvaluateSecret(options.ApiToken);
                 if (string.IsNullOrWhiteSpace(token))
                 {
                     throw new Exception("Czechia API token missing");
                 }

                 client.DefaultRequestHeaders.Remove("AuthorizationToken");
                 client.DefaultRequestHeaders.Add("AuthorizationToken", token);
                 return client;
             }

             private static string NormalizeZone(string zone) => zone.Trim().TrimEnd('.');

             private static string RelativeHost(string zone, string fqdn)
             {
                 zone = NormalizeZone(zone);
                 fqdn = fqdn.Trim().TrimEnd('.');

                 if (string.Equals(fqdn, zone, StringComparison.OrdinalIgnoreCase))
                 {
                     return "@";
                 }

                 var suffix = "." + zone;
                 if (fqdn.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                 {
                     return fqdn.Substring(0, fqdn.Length - suffix.Length);
                 }

                 // fallback: pokud někdo zadá zónu špatně, pošleme celé jméno
                 return fqdn;
             }

             private sealed record TxtBody(string hostName, string text, int ttl, int publishZone);

             public override async Task<bool> CreateRecord(DnsValidationRecord record)
             {
                 try
                 {
                     var zone = NormalizeZone(options.ZoneName);
                     var host = RelativeHost(zone, record.Authority.Domain);

                     var ctx = await GetClient();
                     var url = $"{options.ApiBaseUri.TrimEnd('/')}/DNS/{Uri.EscapeDataString(zone)}/TXT";
                     var body = new TxtBody(host, record.Value, options.Ttl, publishZone: 1);

                     using var resp = await ctx.PostAsJsonAsync(url, body).ConfigureAwait(false);
                     if (!resp.IsSuccessStatusCode)
                     {
                         var msg = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                         _log.Error($"Czechia DNS POST failed: {(int)resp.StatusCode} {resp.ReasonPhrase} {msg}");
                         return false;
                     }

                     return true;
                 }
                 catch (Exception ex)
                 {
                     _log.Error(ex, "Unhandled exception when attempting to create record");
                     return false;
                 }
             }

             public override async Task DeleteRecord(DnsValidationRecord record)
             {
                 try
                 {
                     var zone = NormalizeZone(options.ZoneName);
                     var host = RelativeHost(zone, record.Authority.Domain);

                     var ctx = await GetClient();
                     var url = $"{options.ApiBaseUri.TrimEnd('/')}/DNS/{Uri.EscapeDataString(zone)}/TXT";
                     var body = new TxtBody(host, record.Value, options.Ttl, publishZone: 1);

                     using var req = new HttpRequestMessage(HttpMethod.Delete, url)
                     {
                         Content = JsonContent.Create(body)
                     };

                     using var resp = await ctx.SendAsync(req).ConfigureAwait(false);
                     if (!resp.IsSuccessStatusCode)
                     {
                         var msg = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                         _log.Warning($"Czechia DNS DELETE failed: {(int)resp.StatusCode} {resp.ReasonPhrase} {msg}");
                     }
                 }
                 catch (Exception ex)
                 {
                     _log.Warning(ex, "Unable to delete record");
                 }
             }
         }
}
