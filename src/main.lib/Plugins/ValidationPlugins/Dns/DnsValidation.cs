using ACMESharp.Authorizations;
using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Context;
using PKISharp.WACS.Services;
using System;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    /// <summary>
    /// Base implementation for DNS-01 validation plugins
    /// </summary>reee
    public abstract class DnsValidation<TPlugin>(
        LookupClientProvider dnsClient,
        ILogService log,
        ISettings settings) : Validation<IDnsChallengeValidationDetails>
    {
        protected readonly LookupClientProvider _dnsClient = dnsClient;
        protected readonly ILogService _log = log;
        protected readonly ISettings _settings = settings;
        private readonly ConcurrentBag<DnsValidationRecord> _recordsCreated = [];

        /// <summary>
        /// Calculate the record name for a PERSIST-01 challenge
        /// </summary>
        /// <param name="context"></param>
        /// <param name="challenge"></param>
        private static DnsPersist01ChallengeValidationDetails PreparePersistRecord(ValidationContext context, DnsPersist01ChallengeValidationDetails challenge)
        {
            challenge.DnsRecordName = $"{DnsPersist01ChallengeValidationDetails.DnsRecordNamePrefix}.{context.Identifier}";
            challenge.DnsRecordType = DnsPersist01ChallengeValidationDetails.DnsRecordTypeDefault;

            // Specification:
            // https://datatracker.ietf.org/doc/html/draft-ietf-acme-dns-persist-02#section-10.2
            
            var hashBytes = new List<byte>();
            hashBytes.Add((byte)context.Identifier.Length);
            hashBytes.AddRange(Encoding.ASCII.GetBytes(context.Identifier));
            hashBytes.AddRange(context.Account.Signer.JwsTool().GetThumbprint());
            hashBytes.AddRange(Encoding.ASCII.GetBytes(context.Account.Details.Kid));

            var sha256hash = SHA256.HashData(hashBytes.ToArray());
            var hash = Base64Url.EncodeToString(sha256hash);
            var prefix = context.DirectoryMeta?.AccountHashPrefix ?? throw new InvalidOperationException("Missing account hash prefix.");
            challenge.DnsRecordValue = $"{challenge.IssuerDomainNames.First()}; accounturi={prefix}/sha256/{hash}";
            return challenge;
        }

        /// <summary>
        /// Prepare to add a new DNS record
        /// </summary>
        /// <param name="context"></param>
        /// <param name="challenge"></param>
        /// <returns></returns>
        internal override async Task<bool> PrepareChallenge(ValidationContext context, IDnsChallengeValidationDetails challenge)
        {
            // Support the DNS-PERSIST-01 challenge type
            if (challenge is DnsPersist01ChallengeValidationDetails persist)
            {
                challenge = PreparePersistRecord(context, persist);
            }

            // Check for substitute domains
            var authority = await _dnsClient.GetAuthority(
                challenge.DnsRecordName,
                followCnames: _settings.Validation.AllowDnsSubstitution);

            var success = false;
            while (!success)
            {
                _log.Debug("[{identifier}] Attempting to create DNS record under {authority}...", context.Label, authority.Domain);
                var record = new DnsValidationRecord(context, authority, challenge.DnsRecordValue);
                success = await CreateRecord(record);
                if (!success)
                {
                    _log.Debug("[{identifier}] Failed to create record under {authority}", context.Label, authority.Domain);
                    authority = authority.From ?? throw new Exception($"[{context.Label}] Unable to prepare for challenge answer");
                } 
                else
                {
                    _log.Information("[{identifier}] Record {value} successfully created", context.Label, record.Value);
                    _recordsCreated.Add(record);
                }
            }
            return true;
        }

        /// <summary>
        /// Default commit function, doesn't do anything because 
        /// default doesn't do parallel operation
        /// </summary>
        /// <returns></returns>
        public override sealed async Task Commit()
        {
            // Wait for changes to be saved
            await SaveChanges();

            // Verify that the record was created successfully and wait for possible
            // propagation/caching/TTL issues to resolve themselves naturally
            if (_settings.Validation.PreValidateDns)
            {
                var validationTasks = _recordsCreated.Select(ValidateRecord);
                await Task.WhenAll(validationTasks);
            }
            if (_settings.Validation.DnsPropagationDelay > 0)
            {
                _log.Information("Waiting {n} seconds for global DNS propagation...", _settings.Validation.DnsPropagationDelay);
                await Task.Delay(_settings.Validation.DnsPropagationDelay * 1000);
            }
        }

        /// <summary>
        /// Typically the changes will already be saved by 
        /// PrepareChallenge, but for those plugins that support
        /// parallel operation, this may be overridden to handle
        /// persistance
        /// </summary>
        /// <returns></returns>
        public virtual Task SaveChanges() => Task.CompletedTask;

        /// <summary>
        /// Check the TXT value from all known authoritative DNS servers
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        internal async Task<bool> PreValidate(DnsValidationRecord record)
        {
            var success = 0;
            var count = record.Authority.Nameservers.Count();
            try
            {
                _log.Debug("[{identifier}] Looking for TXT value {DnsRecordValue}...", record.Label, record.Value);
                var testClients = record.Authority.Nameservers;
                if (_settings.Validation.PreValidateDnsLocal)
                {
                    testClients = testClients.Append(_dnsClient.GetSystemClient());
                }
                foreach (var client in testClients)
                {
                    _log.Debug("[{identifier}] [{ip}] Getting TXT records...", record.Label, client.IpAddress);
                    var answers = await client.GetTxtRecords(record.Authority.Domain);
                    if (!answers.Any())
                    {
                        _log.Warning("[{identifier}] [{ip}] No TXT records found", record.Label, client.IpAddress);
                        continue;
                    }
                    if (!answers.Contains(record.Value))
                    {
                        _log.Debug("[{identifier}] [{ip}] Found {answers}", record.Label, client.IpAddress, answers);
                        _log.Warning("[{identifier}] [{ip}] Incorrect TXT record(s) found", record.Label, client.IpAddress);
                        continue;
                    }
                    _log.Debug("[{identifier}] [{ip}] looks good", record.Label, client.IpAddress);
                    success++;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "[{identifier}] Preliminary validation failed", record.Label);
                return false;
            }
            if (success == count)
            {
                _log.Information("[{identifier}] Preliminary validation succeeded", record.Label);
                return true;
            }
            if (success >= 1)
            {
                _log.Information("[{identifier}] Preliminary validation failed on {n}/{m} nameservers", record.Label, success, count);
            } 
            else
            {
                _log.Information("[{identifier}] Preliminary validation failed on all nameservers", record.Label);
            }
            return false;
        }

        /// <summary>
        /// Delete record when we're done
        /// </summary>
        public override sealed async Task CleanUp()
        {
            foreach (var record in _recordsCreated)
            {
                try
                {
                    await DeleteRecord(record);
                    _log.Information("[{identifier}] Record {value} deleted", record.Label, record.Value);
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "[{identifier}] Error deleting record {value}", record.Label, record.Value);
                }
            }
            await Finalize();
            _recordsCreated.Clear();
            _log.Debug("DNS records cleaned up");
        }

        /// <summary>
        /// Typically the changes will already be undone by 
        /// Finalize, but for those plugins that support
        /// parallel operation, this may be overridden 
        /// </summary>
        /// <returns></returns>
        public virtual Task Finalize() => Task.CompletedTask;

        /// <summary>
        /// Validate a record as being correctly created an sychronised, runs during/after the commit state
        /// </summary>
        /// <param name="record"></param>
        /// <returns></returns>
        private async Task ValidateRecord(DnsValidationRecord record)
        {
            var retry = 0;
            var maxRetries = _settings.Validation.PreValidateDnsRetryCount;
            var retrySeconds = _settings.Validation.PreValidateDnsRetryInterval;
            while (true)
            {
                if (await PreValidate(record))
                {
                    break;
                }
                else
                {
                    retry += 1;
                    if (retry > maxRetries)
                    {
                        _log.Information("[{identifier}] It looks like validation is going to fail, but we will try now anyway...", record.Label);
                        break;
                    }
                    else
                    {
                        _log.Information("[{identifier}] Will retry in {s} seconds (retry {i}/{j})...", record.Label, retrySeconds, retry, maxRetries);
                        await Task.Delay(retrySeconds * 1000);
                    }
                }
            }
        }

        /// <summary>
        /// Delete validation record
        /// </summary>
        /// <param name="recordName">Name of the record</param>
        internal virtual Task DeleteRecord(ValidationContext context, DnsValidationRecord record) => DeleteRecord(record);

        /// <summary>
        /// Delete validation record
        /// </summary>
        /// <param name="recordName">Name of the record</param>
        public virtual Task DeleteRecord(DnsValidationRecord record) => Task.CompletedTask;

        /// <summary>
        /// Create validation record
        /// </summary>
        /// <param name="recordName">Name of the record</param>
        internal virtual Task CreateRecord(ValidationContext context, DnsValidationRecord record) => CreateRecord(record);

        /// <summary>
        /// Create validation record
        /// </summary>
        /// <param name="recordName">Name of the record</param>
        /// <param name="token">Contents of the record</param>
        public abstract Task<bool> CreateRecord(DnsValidationRecord record);

        /// <summary>
        /// Match DNS zone to use from a list of all zones
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="candidates"></param>
        /// <param name="recordName"></param>
        /// <returns></returns>
        public T? FindBestMatch<T>(Dictionary<string, T> candidates, string recordName) where T: class
        {
            var result = candidates.Keys.Select(key =>
            {
                var fit = 0;
                var name = key.TrimEnd('.');
                if (string.Equals(recordName, name, StringComparison.InvariantCultureIgnoreCase) || 
                    recordName.EndsWith("." + name, StringComparison.InvariantCultureIgnoreCase))
                {
                    // If there is a zone for a.b.c.com (4) and one for c.com (2)
                    // then the former is a better (more specific) match than the
                    // latter, so we should use that
                    fit = name.Split('.').Length;
                    _log.Verbose("Zone {name} scored {fit} points", key, fit);
                }
                else
                {
                    _log.Verbose("Zone {name} not matched", key);
                }
                return new { 
                    key, 
                    value = candidates[key],
                    fit
                };
            }).
            Where(x => x.fit > 0).
            OrderByDescending(x => x.fit).
            FirstOrDefault();

            if (result != null)
            {
                _log.Debug("Picked {name} as best match", result.key);
                return result.value;
            } 
            else
            {
                _log.Error("No match found");
                return null;
            }
        }

        /// <summary>
        /// Translate full host name to zone relative name
        /// 
        /// recordName: _acme-challenge.sub.domain.com
        /// zone: domain.com
        /// return: _acme-challenge.sub
        /// 
        /// recordName: domain.com
        /// zone: domain.com
        /// return: @
        /// </summary>
        /// <param name="zone"></param>
        /// <param name="recordName"></param>
        /// <returns></returns>
        public static string RelativeRecordName(string zone, string recordName)
        {
            var index = recordName.LastIndexOf(zone);
            if (index == -1) {
                throw new InvalidOperationException($"Cannot create {recordName} in zone {zone}");
            }
            var ret = recordName[..index].TrimEnd('.');
            return string.IsNullOrEmpty(ret) ? "@" : ret;
        }

        /// <summary>
        /// Keep track of which records are created, so that they can be deleted later
        /// </summary>
        public class DnsValidationRecord
        {
            internal DnsValidationRecord(ValidationContext context, DnsLookupResult authority, string value)
            {
                Identifier = context.Identifier;
                Label = context.Label;
                Authority = authority;
                Value = value;
            }

            public string Label { get; }
            public string Identifier { get; }
            public DnsLookupResult Authority { get; }
            public string Value { get; }
        }
    }

    public abstract class DnsValidation<TPlugin, TClient>(LookupClientProvider dnsClient, ILogService log, ISettings settings, IProxyService proxy) : 
        DnsValidation<TPlugin>(dnsClient, log, settings), IDisposable 
        where TClient: class
    {
        protected IProxyService _proxy = proxy;

        private HttpClient? _httpClient = default;
        protected async Task<HttpClient> GetHttpClient()
        {
            if (_httpClient == default)
            {
                _httpClient = await _proxy.GetHttpClient();
            }
            return _httpClient;
        }

        private TClient? _cachedClient = default;
        protected async Task<TClient> GetClient()
        {
            if (_cachedClient == default) {
                _log.Debug("Client of type {x} created", typeof(TClient).Name);
                var httpClient = await GetHttpClient();
                _cachedClient = await CreateClient(httpClient);
            }
            return _cachedClient;
        }
        protected internal abstract Task<TClient> CreateClient(HttpClient httpClient);

        public void Dispose()
        {
            if (_cachedClient is IDisposable disposable)
            {
                _log.Debug("Client of type {x} disposed", typeof(TClient).Name);
                disposable?.Dispose();
            }
            _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
