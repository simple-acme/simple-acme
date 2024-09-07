using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.TargetPlugins
{
    [SupportedOSPlatform("windows")]
    [IPlugin.Plugin<
        IISSitesOptions, IISSitesOptionsFactory, 
        IISCapability, WacsJsonPlugins>
        ("cdd79a68-4a87-4039-bee8-5a0ebdca41cb", 
        "IISSites", "Read sites from IIS (legacy)", Hidden = true)]
    [IPlugin.Plugin<
        IISSiteOptions, IISSiteOptionsFactory,
        IISCapability, WacsJsonPlugins>
        ("d7940b23-f570-460e-ab15-2c822a79009b", 
        "IISSite", "Read site from IIS (legacy)", Hidden = true)]
    [IPlugin.Plugin<
        IISBindingOptions, IISBindingOptionsFactory, 
        IISCapability, WacsJsonPlugins>
        ("2f5dd428-0f5d-4c8a-8fd0-56fc1b5985ce", 
        "IISBinding", "Read bindings from IIS (legacy)", Hidden = true)]
    [IPlugin.Plugin1<
        IISOptions, IISOptionsFactory, 
        IISCapability, WacsJsonPlugins, IISArguments>
        ("54deb3ee-b5df-4381-8485-fe386054055b", 
        "IIS", "Read bindings from IIS", Name = "IIS bindings")]
    internal class IIS(ILogService logService, IISHelper helper, IISOptions options) : ITargetPlugin
    {
        public Task<Target?> Generate()
        {
            // Check if we have any bindings
            var allBindings = helper.GetBindings();
            var filteredBindings = helper.FilterBindings(allBindings, options);
            if (filteredBindings.Count == 0)
            {
                logService.Error("No bindings matched, unable to proceed");
                return Task.FromResult<Target?>(null);
            }

            // Handle common name
            var cn = options.CommonName ?? "";
            var cnDefined = !string.IsNullOrWhiteSpace(cn);
            var cnBinding = default(IISHelper.IISBindingOption); 
            if (cnDefined)
            {
                cnBinding = filteredBindings.FirstOrDefault(x => x.HostUnicode == cn);
            }
            var cnValid = cnDefined && cnBinding != null;
            if (cnDefined && !cnValid)
            {
                logService.Warning("Specified common name {cn} not valid", cn);
            }

            // Generate friendly name suggestion
            var friendlyNameSuggestion = "[IIS]";
            if (options.IncludeSiteIds != null && options.IncludeSiteIds.Count != 0)
            {
                var sites = helper.GetSites(false);
                var site = default(IISHelper.IISSiteOption);
                if (cnBinding != null)
                {
                    site = sites.FirstOrDefault(s => s.Id == cnBinding.SiteId);
                } 
                site ??= sites.FirstOrDefault(x => options.IncludeSiteIds.Contains(x.Id));
                var count = options.IncludeSiteIds.Count;
                if (site != null)
                {
                    friendlyNameSuggestion += $" {site.Name}";
                    count -= 1;
                }
                if (count > 0)
                {
                    friendlyNameSuggestion += $" (+{count} other{(count == 1 ? "" : "s")})";
                } 
            }
            else
            {
                friendlyNameSuggestion += $" (any site)";
            }

            if (!string.IsNullOrEmpty(options.IncludePattern))
            {
                friendlyNameSuggestion += $" | {options.IncludePattern}";
            }
            else if (options.IncludeHosts != null && options.IncludeHosts.Count != 0)
            {
                var host = default(string);
                if (cnBinding != null)
                {
                    host = cnBinding.HostUnicode;
                }
                host ??= options.IncludeHosts.First();
                friendlyNameSuggestion += $", {host}";
                var count = options.IncludeHosts.Count;
                if (count > 1)
                {
                    friendlyNameSuggestion += $" (+{count - 1} other{(count == 2 ? "" : "s")})";
                }
            }
            else if (options.IncludeRegex != null)
            {
                friendlyNameSuggestion += $", {options.IncludeRegex}";
            }
            else
            {
                friendlyNameSuggestion += $", (any host)";
            }

            // Return result
            var commonName = cnValid ? 
                cn : 
                filteredBindings.
                    Where(x => x.HostUnicode.Length <= Constants.MaxCommonName).
                    FirstOrDefault()?.
                    HostUnicode;
            var parts = filteredBindings.
                GroupBy(x => x.SiteId).
                Select(group => new TargetPart(group.Select(x => new DnsIdentifier(x.HostUnicode)))
                {
                    SiteId = group.Key,
                    SiteType = group.First().SiteType
                });
            return Task.FromResult<Target?>(new Target(friendlyNameSuggestion, commonName, parts.ToList()));
        }
    }
}