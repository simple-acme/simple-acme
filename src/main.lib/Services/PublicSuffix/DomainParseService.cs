using Nager.PublicSuffix;
using Nager.PublicSuffix.RuleProviders;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    public partial class DomainParseService(ILogService log, IProxyService proxy, ISettings settings)
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
            var update = _settings.Acme.PublicSuffixListUri;
            if (update != null)
            {
                try
                {
                    var webProvider = new WebTldRuleProvider(update, _proxy, _log, _settings);
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
            return new DomainParser(provider, new Normalizer());
        }

        private DomainInfo? GetParseResult(string fulldomain)
        {
            if (_parser == null)
            {
                throw new InvalidOperationException("DomainParseService is not initialized");
            }
            try
            {
                return _parser.Parse(fulldomain);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Unable to determine toplevel/registerable part for {fulldomain}", fulldomain);
                return null;          
            }
        }

        public string GetTLD(string fulldomain) => GetParseResult(fulldomain)?.TopLevelDomain ?? fulldomain;
        public string GetRegisterableDomain(string fulldomain) => GetParseResult(fulldomain)?.RegistrableDomain ?? fulldomain;

    }
}
