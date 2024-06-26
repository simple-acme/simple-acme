using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.TargetPlugins;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace PKISharp.WACS.Clients.IIS
{
    internal class IISHelper(ILogService log, IIISClient iisClient, DomainParseService domainParser)
    {
        internal class IISBindingOption(string hostUnicode, string hostPunycode)
        {
            public long SiteId { get; set; }
            public IISSiteType SiteType { get; set; }
            public bool Secure { get; set; }
            public bool Wildcard => HostUnicode.StartsWith("*.");
            public string HostUnicode { get; private set; } = hostUnicode;
            public string HostPunycode { get; private set; } = hostPunycode;
            public int Port { get; set; }
            public string? Protocol { get; set; }

            public override string ToString()
            {
                if ((Protocol == "http" && Port != 80) ||
                    (Protocol == "https" && Port != 443))
                {
                    return $"{HostUnicode}:{Port} (Site {SiteId}, {Protocol})";
                }
                return $"{HostUnicode} (Site {SiteId})";
            }
        }

        internal class IISSiteOption(string name, IEnumerable<string> hosts)
        {
            public long Id { get; set; }
            public IISSiteType SiteType { get; set; }
            public string Name { get; } = name;
            public bool Secure { get; set; }
            public List<string> Hosts { get; } = hosts.ToList();
        }

        public const string WebTypeFilter = "http";
        public const string FtpTypeFilter = "ftp";
        private readonly IdnMapping _idnMapping = new();
        internal DomainParseService DomainParser { get; } = domainParser;

        internal List<IISBindingOption> GetBindings()
        {
            if (iisClient.Version.Major == 0)
            {
                log.Warning("IIS not found. Skipping scan.");
                return [];
            }
            return [.. GetBindings(iisClient.Sites)];
        }

        private List<IISBindingOption> GetBindings(IEnumerable<IIISSite> sites)
        {
            // Get all bindings matched together with their respective sites
            log.Debug("Scanning IIS bindings for host names");
            var siteBindings = sites.
                SelectMany(site => site.Bindings, (site, binding) => new { site, binding }).
                Where(sb => !string.IsNullOrWhiteSpace(sb.binding.Host)).
                ToList();

            static string lookupKey(IIISSite site, IIISBinding binding) =>
                site.Id + "#" + binding.BindingInformation.ToLower();

            // Option: hide http bindings when there are already https equivalents
            var secure = siteBindings
                .Where(sb =>
                    sb.binding.Secure ||
                    sb.site.Bindings.Any(other =>
                        other.Secure &&
                        string.Equals(sb.binding.Host, other.Host, StringComparison.InvariantCultureIgnoreCase)))
                .ToDictionary(sb => lookupKey(sb.site, sb.binding));

            var targets = siteBindings.
                Select(sb => new
                {
                    host = sb.binding.Host.ToLower(),
                    sb.site,
                    sb.binding,
                    secure = secure.ContainsKey(lookupKey(sb.site, sb.binding))
                }).
                Select(sbi => new IISBindingOption(sbi.host, _idnMapping.GetAscii(sbi.host))
                {
                    SiteId = sbi.site.Id,
                    SiteType = sbi.site.Type,
                    Port = sbi.binding.Port,
                    Protocol = sbi.binding.Protocol,
                    Secure = sbi.secure
                }).
                DistinctBy(t => t.HostUnicode + "@" + t.SiteId).
                ToList();

            return targets;
        }

        internal List<IISSiteOption> GetSites(bool logInvalidSites)
        {
            if (iisClient.Version.Major == 0)
            {
                log.Warning("IIS not found. Skipping scan.");
                return [];
            }
            // Get all bindings matched together with their respective sites
            log.Debug("Scanning IIS sites");
            var targets = GetSites(iisClient.Sites).ToList();
            if (targets.Count == 0 && logInvalidSites)
            {
                log.Warning("No applicable IIS sites were found.");
            }
            return targets;
        }

        private static List<IISSiteOption> GetSites(IEnumerable<IIISSite> sites)
        {
            // Get all bindings matched together with their respective sites
            var secure = sites.Where(site =>
                site.Bindings.All(binding =>
                    binding.Secure ||
                    site.Bindings.Any(other =>
                        other.Secure &&
                        string.Equals(other.Host, binding.Host, StringComparison.InvariantCultureIgnoreCase)))).ToList();

            var targets = sites.
                Select(site => new IISSiteOption(site.Name, GetHosts(site))
                {
                    Id = site.Id,
                    SiteType = site.Type,
                    Secure = secure.Contains(site)
                }).
                ToList();
            return targets;
        }

        internal List<IISBindingOption> FilterBindings(List<IISBindingOption> bindings, IISOptions options)
        {
            // Check if we have any bindings
            log.Verbose("{0} named bindings found in IIS", bindings.Count);

            // Filter by binding/site type
            log.Debug("Filtering based on binding type");
                bindings = bindings.Where(x => {
                    return x.SiteType switch
                    {
                        IISSiteType.Web => options.IncludeTypes == null || options.IncludeTypes.Contains(WebTypeFilter),
                        IISSiteType.Ftp => options.IncludeTypes != null && options.IncludeTypes.Contains(FtpTypeFilter),
                        _ => false
                    };
                }). 
                ToList();

            // Filter by site
            if (options.IncludeSiteIds != null && options.IncludeSiteIds.Count != 0)
            {
                log.Debug("Filtering by site(s) {0}", options.IncludeSiteIds);
                bindings = bindings.Where(x => options.IncludeSiteIds.Contains(x.SiteId)).ToList();
                log.Verbose("{0} bindings remaining after site filter", bindings.Count);
            }
            else
            {
                log.Verbose("No site filter applied");
            }

            // Filter by pattern
            var regex = GetRegex(options);
            if (regex != null)
            {
                log.Debug("Filtering by host: {regex}", regex);
                bindings = bindings.Where(x => Matches(x, regex)).ToList();
                log.Verbose("{0} bindings remaining after host filter", bindings.Count);
            }
            else
            {
                log.Verbose("No host filter applied");
            }

            // Remove exlusions
            if (options.ExcludeHosts != null && options.ExcludeHosts.Count != 0)
            {
                bindings = bindings.Where(x => !options.ExcludeHosts.Contains(x.HostUnicode)).ToList();
                log.Verbose("{0} named bindings remaining after explicit exclusions", bindings.Count);
            }

            // Check if we have anything left
            log.Verbose($"{{count}} matching binding{(bindings.Count != 1 ? "s" : "")} found", bindings.Count);
            return [.. bindings];
        }

        internal static bool Matches(IISBindingOption binding, Regex regex)
        {
            return regex.IsMatch(binding.HostUnicode)
                || regex.IsMatch(binding.HostPunycode);
        }

        internal static string HostsToRegex(IEnumerable<string> hosts) =>
            $"^({string.Join('|', hosts.Select(Regex.Escape))})$";

        private static Regex? GetRegex(IISOptions options)
        {
            if (!string.IsNullOrEmpty(options.IncludePattern))
            {
                return new Regex(options.IncludePattern.PatternToRegex());
            }
            if (options.IncludeHosts != null && options.IncludeHosts.Count != 0)
            {
                return new Regex(HostsToRegex(options.IncludeHosts));
            }
            if (!string.IsNullOrEmpty(options.IncludeRegex))
            {
                return new Regex(options.IncludeRegex);
            }
            return null;
        }

        private static List<string> GetHosts(IIISSite site)
        {
            return site.Bindings.Select(x => x.Host.ToLower()).
                            Where(x => !string.IsNullOrWhiteSpace(x)).
                            Distinct().
                            ToList();
        }
    }
}
