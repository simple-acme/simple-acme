using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.OrderPlugins
{
    [IPlugin.Plugin<
        DomainOptions, PluginOptionsFactory<DomainOptions>,
        DomainCapability, WacsJsonPlugins>
        ("b7c331d4-d875-453e-b83a-2b537ca12535", 
        "Domain", "Separate certificate for each registerable domain (e.g. *.example.com)")]
    internal class Domain(DomainParseService domainParseService, ILogService log) : IOrderPlugin
    {
        public List<Order> Split(Renewal renewal, Target target) 
        {
            var ret = new Dictionary<string, Order>();
            var parts = new Dictionary<string, List<TargetPart>>();
            foreach (var part in target.Parts)
            {
                foreach (var host in part.GetIdentifiers(true))
                {
                    var domain = host.Value;
                    switch (host)
                    {
                        case DnsIdentifier dns:
                            domain = domainParseService.GetRegisterableDomain(host.Value.TrimStart('.', '*'));
                            break;
                        default:
                            log.Warning("Unsupported identifier type {type}", host.Type);
                            break;
                    }
                    var sourceParts = target.Parts.Where(p => p.GetIdentifiers(true).Contains(host));
                    if (!ret.ContainsKey(domain))
                    {
                        var filteredParts = sourceParts.Select(p =>
                            new TargetPart([host]) {
                                SiteId = p.SiteId, 
                                SiteType = p.SiteType 
                            }).ToList();
                        var newTarget = new Target(
                            target.FriendlyName,
                            host.Value.Length <= Constants.MaxCommonName ? host : null,
                            filteredParts);
                        var newOrder = new Order(
                            renewal, 
                            newTarget, 
                            friendlyNamePart: domain,
                            cacheKeyPart: domain);
                        ret.Add(domain, newOrder);
                        parts.Add(domain, filteredParts);
                    }
                    else
                    {
                        var existingParts = parts[domain];
                        foreach (var sourcePart in sourceParts)
                        {
                            var existingPart = existingParts.Where(x => sourcePart.SiteId == x.SiteId).FirstOrDefault();
                            if (existingPart == null)
                            {
                                existingPart = new TargetPart([host]) {
                                    SiteId = sourcePart.SiteId,
                                    SiteType = sourcePart.SiteType
                                };
                                existingParts.Add(existingPart);
                            } 
                            else if (!existingPart.Identifiers.Contains(host))
                            {
                                existingPart.Identifiers.Add(host);
                            }
                        }
                    } 
                }
            }
            return [.. ret.Values];
        }
    }
}
