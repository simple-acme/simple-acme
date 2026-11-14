using Nager.PublicSuffix.DomainNormalizers;
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Services
{
    public partial class DomainParseService
    {
        /// <summary>
        /// Regular 30 day file cache in the configuration folder
        /// </summary>
        private class Normalizer : IDomainNormalizer
        {
            public List<string>? PartlyNormalizeDomainAndExtractFullyNormalizedParts(string domain, out string? partlyNormalizedDomain) 
            {
                partlyNormalizedDomain = null;
                if (string.IsNullOrWhiteSpace(domain))
                {
                    return null;
                }
                domain = domain.TrimEnd('.');
                if (string.IsNullOrEmpty(domain))
                {
                    return null;
                }
                partlyNormalizedDomain = domain.ToLowerInvariant();
                return [.. partlyNormalizedDomain.Split('.').Reverse()];
            }
        }
    }
}
