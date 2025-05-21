using Nager.PublicSuffix;
using Nager.PublicSuffix.Exceptions;
using Nager.PublicSuffix.Extensions;
using Nager.PublicSuffix.Models;
using Nager.PublicSuffix.RuleParsers;
using Nager.PublicSuffix.RuleProviders;
using Nager.PublicSuffix.RuleProviders.CacheProviders;
using PKISharp.WACS.Extensions;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    public class DomainParseService(ILogService log, IProxyService proxy, ISettings settings)
    {
        private DomainParser? _parser;
        private readonly ILogService _log = log;
        private readonly ISettings _settings = settings;
        private readonly IProxyService _proxy = proxy;

        public async Task Initialize() => _parser ??= await CreateParser();

        private async Task<DomainParser> CreateParser()
        {
            var provider = default(IRuleProvider);
            var path = Path.Combine(VersionService.ResourcePath, "public_suffix_list.dat");
            try
            {
                var fileProvider = new LocalFileRuleProvider(path);
                if (await fileProvider.BuildAsync())
                {
                    provider = fileProvider;
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error loading static public suffix list from {path}", path);
            }
            var update = _settings.Acme.PublicSuffixListUri ?? new Uri("https://publicsuffix.org/list/public_suffix_list.dat");
            if (update.ToString() != "")
            {
                try
                {
                    var webProvider = new WebTldRuleProvider(_proxy, _log, _settings);
                    if (await webProvider.BuildAsync())
                    {
                        provider = webProvider;
                    }
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "Error updating public suffix list from {source}", update);
                }
            }
            if (provider == null)
            {
                throw new Exception("Public suffix list unavailable");
            }
            return new DomainParser(provider);
        }

        private DomainInfo? GetParseResult(string fulldomain)
        {
            if (_parser == null)
            {
                throw new InvalidOperationException("DomainParseService is not initialized");
            }
            return _parser.Parse(fulldomain);
        }

        public string GetTLD(string fulldomain) => GetParseResult(fulldomain)?.TopLevelDomain ?? fulldomain;
        public string GetRegisterableDomain(string fulldomain) => GetParseResult(fulldomain)?.RegistrableDomain ?? fulldomain;

        /// <summary>
        /// Regular 30 day file cache in the configuration folder
        /// </summary>
        private class FileCacheProvider : ICacheProvider
        {
            private readonly FileInfo? _file;
            private string? _memoryCache;
            private readonly ILogService _log;

            public FileCacheProvider(ILogService log, ISettings settings)
            {
                _log = log;
                var path = Path.Combine(settings.Client.ConfigurationPath, "public_suffix_list.dat");
                _file = new FileInfo(path);
            }

            public async Task<string?> GetAsync()
            {
                if (_file != null)
                {
                    try
                    {
                        _memoryCache = await File.ReadAllTextAsync(_file.FullName);
                    }
                    catch (Exception ex)
                    {
                        _log.Warning(ex, "Unable to read public suffix list cache from {path}", _file.FullName);
                    };
                }
                return _memoryCache;
            }

            public bool IsCacheValid()
            {
                if (_file != null)
                {
                    return _file.Exists && _file.LastWriteTimeUtc > DateTime.UtcNow.AddDays(-30);
                }
                else
                {
                    return !string.IsNullOrEmpty(_memoryCache);
                }
            }

            public async Task SetAsync(string val) 
            {
                if (_file != null)
                {
                    try
                    {
                        await _file.SafeWrite(val);
                    } 
                    catch (Exception ex)
                    {
                        _log.Warning(ex, "Unable to write public suffix list cache to {path}", _file.FullName);
                    }
                }
                _memoryCache = val;
            }
        }

        /// <summary>
        /// Custom implementation so that we can use the proxy 
        /// that the user has configured and 
        /// </summary>
        private class WebTldRuleProvider(IProxyService proxy, ILogService log, ISettings settings) : IRuleProvider
        {
            private readonly string _fileUrl = "https://publicsuffix.org/list/public_suffix_list.dat";
            private readonly FileCacheProvider _cache = new(log, settings);
            private readonly DomainDataStructure _data = new("*", new TldRule("*"));

            public async Task<bool> BuildAsync(bool ignoreCache = false, CancellationToken cancel = default)
            {
                string? ruleData;
                if (ignoreCache || !_cache.IsCacheValid())
                {
                    ruleData = await LoadFromUrl(_fileUrl);
                    await _cache.SetAsync(ruleData);
                }
                else
                {
                    ruleData = await _cache.GetAsync();
                }
                if (string.IsNullOrEmpty(ruleData))
                {
                    return false;
                }
                var ruleParser = new TldRuleParser(TldRuleDivisionFilter.ICANNOnly);
                var enumerable = ruleParser.ParseRules(ruleData);
                _data.AddRules(enumerable);
                return true;
            }

            public DomainDataStructure? GetDomainDataStructure() => _data;

            public async Task<string> LoadFromUrl(string url)
            {
                using var httpClient = await proxy.GetHttpClient();
                using var response = await httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    throw new RuleLoadException($"Cannot load from {url} {response.StatusCode}");
                }
                return await response.Content.ReadAsStringAsync();
            }
        }

    }
}
