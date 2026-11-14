using Nager.PublicSuffix.Exceptions;
using Nager.PublicSuffix.Extensions;
using Nager.PublicSuffix.Models;
using Nager.PublicSuffix.RuleParsers;
using Nager.PublicSuffix.RuleProviders;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    public partial class DomainParseService
    {
        /// <summary>
        /// Custom implementation so that we can use the proxy 
        /// that the user has configured and 
        /// </summary>
        private class WebTldRuleProvider(Uri url, IProxyService proxy, ILogService log, ISettings settings) : IRuleProvider
        {
            private readonly FileCacheProvider _cache = new(log, settings);
            private readonly DomainDataStructure _data = new("*", new TldRule("*"));

            public async Task<bool> BuildAsync(bool ignoreCache = false, CancellationToken cancel = default)
            {
                string? ruleData;
                if (ignoreCache || !_cache.IsCacheValid())
                {
                    ruleData = await LoadFromUrl(url.ToString());
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
