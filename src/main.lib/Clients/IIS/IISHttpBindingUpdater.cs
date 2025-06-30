using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Clients.IIS
{
    /// <summary>
    /// Modifies IIS bindings
    /// </summary>
    /// <remarks>
    /// Constructore
    /// </remarks>
    /// <param name="client"></param>
    internal partial class IISHttpBindingUpdater<TSite, TBinding>(
        IIISClient<TSite, TBinding> client,
        ILogService log)
        where TSite : IIISSite<TBinding>
        where TBinding : IIISBinding
    {
        /// <summary>
        /// Update/create bindings for all host names in the identifier
        /// </summary>
        /// <param name="target"></param>
        /// <param name="flags"></param>
        /// <param name="thumbprint"></param>
        /// <param name="store"></param>
        public void AddOrUpdateBindings(IISHttpBindingUpdaterContext ctx)
        {
            ctx.AllBindings = GetAllSites();
            ctx.AllIdentifiers ??= ctx.PartIdentifiers;

            // Select bindings that are already connected to the new identifier
            // either due to them being linked to the CentralSsl store, or because
            // the identifier hash already matches. This can be the case because we
            // re-used a identifier from cache, or because the installation plugin
            // issues multiple calls to this function for multiple parts (sites).
            var matchedBindings = ctx.AllBindings.
                Where(sb =>
                {
                    if (sb.binding.Protocol != "https")
                    {
                        return false;
                    }
                    if (sb.binding.CertificateHash == null)
                    {
                        return sb.binding.SSLFlags.HasFlag(SSLFlags.CentralSsl);
                    }
                    else
                    {
                        return StructuralComparisons.StructuralEqualityComparer.Equals(sb.binding.CertificateHash, ctx.BindingOptions.Thumbprint);
                    }
                }).
                ToList();

            // Choose which bindings to replace
            var replaceBindings = ChooseBindingsToReplace(ctx);

            // Update all bindings that we've chosen to replace
            foreach (var (site, binding) in replaceBindings)
            {
                try
                {
                    if (UpdateExistingBindingFlags(
                            ctx.BindingOptions.Flags, 
                            binding, 
                            [.. ctx.AllBindings.Select(v => v.binding)], 
                            out var updateFlags))
                    {
                        var updateOptions = ctx.BindingOptions.WithFlags(updateFlags);
                        var update = UpdateBinding((TSite)site, (TBinding)binding, updateOptions);
                        if (update != null) 
                        {
                            ctx.UpdatedBindings.Add(update.WithSiteId(site.Id).Debug);
                            ctx.AllBindings = GetAllSites(); // Refresh bindings after update
                            matchedBindings.Add((site, binding));
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Error updating binding {host}", binding.BindingInformation);
                    throw;
                }
            }

            if (ctx.BindingOptions.SiteId == null)
            {
                // If we are not adding any new bindings, we can stop here
                // If no installation site has been specified, we cannot add
                // any new bindings.
                return;
            }

            // Find all hostnames which are not covered by any of the already updated
            // bindings yet, because we will want to make sure that those are accessable
            // in the target site
            var targetSite = client.GetSite(ctx.BindingOptions.SiteId.Value, IISSiteType.Web);
            var todo = ctx.PartIdentifiers;
            while (todo.Any())
            {
                // Filter by previously matched bindings
                todo = todo.Where(cert => !matchedBindings.Any(iis => Fits(iis.binding, cert, ctx.BindingOptions.Flags) > 0 && iis.site.Id == ctx.BindingOptions.SiteId));
                if (!todo.Any())
                {
                    break;
                }
                var current = todo.First();
                var addOptions = current switch
                {
                    DnsIdentifier _ => ctx.BindingOptions.WithHost(current.Value),
                    IpIdentifier _ => ctx.BindingOptions.WithIP(current.Value),
                    _ => throw new InvalidOperationException($"Unsupported identifier type {current.GetType().Name} for IIS binding update")
                };

                var newBindings = ChooseBindingsToAdd(current, targetSite, addOptions);
                if (newBindings.Count == 0)
                {
                    // We were unable to create a new binding for this host
                    // but we will still cross it off the list to eventually
                    // break the loop.
                    matchedBindings.Add((targetSite, new DummyBinding(current)));
                    continue;
                }

                foreach (var (site, bindingOptions) in newBindings)
                {
                    try
                    {
                        if (AllowAdd(bindingOptions, ctx.AllBindings.Select(x => x.binding)))
                        {
                            addOptions = bindingOptions.WithFlags(CheckFlags(true, bindingOptions.Host, bindingOptions.Flags));
                            var newBinding = AddBinding(site, addOptions);
                            ctx.AddedBindings.Add(addOptions.Debug);
                            ctx.AllBindings = GetAllSites();
                            matchedBindings.Add((site, newBinding));
                        }
                        else
                        {
                            // We were unable to create the binding because it would
                            // lead to a duplicate. Pretend that we did add it to 
                            // still be able to get out of the loop;
                            matchedBindings.Add((targetSite, new DummyBinding(current)));
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex, "Error creating binding {host}", current);

                        // Prevent infinite retry loop, we just skip the domain when
                        // an error happens creating a new binding for it. User can
                        // always change/add the bindings manually after all.
                        matchedBindings.Add((targetSite, new DummyBinding(current)));
                    }
                }
            }
        }

        /// <summary>
        /// Select which bindings should be updated to match
        /// the new identifier
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        private List<(IIISSite, IIISBinding)> ChooseBindingsToReplace(IISHttpBindingUpdaterContext ctx)
        {
            // Choose which pre-existing https bindings
            // should be replaced with the new identifier
            var replaceBindings = new List<(IIISSite, IIISBinding)>();
            if (ctx.PreviousCertificate != null)
            {
                var thumbMatches = ctx.AllBindings.Where(sb => StructuralComparisons.StructuralEqualityComparer.Equals(sb.binding.CertificateHash, ctx.PreviousCertificate));
                foreach (var sb in thumbMatches)
                {
                    if (ctx.AllIdentifiers?.Any(i => Fits(sb.binding, i, SSLFlags.None) > 0) ?? false)
                    {
                        replaceBindings.Add(sb);
                    }
                    else
                    {
                        log.Warning(
                            "Existing https binding {host}:{port}{ip} not updated because it doesn't seem to match the new identifier!",
                            sb.binding.Host,
                            sb.binding.Port,
                            string.IsNullOrEmpty(sb.binding.IP) ? "" : $":{sb.binding.IP}");
                    }
                }
            }
            
            var filteredBindings = ctx.AllBindings.Where(b => b.binding.Protocol == "https");
            if (ctx.BindingOptions.SiteId != null)
            {
                filteredBindings = filteredBindings.Where(sb => sb.site.Id == ctx.BindingOptions.SiteId.Value);
            }
            foreach (var identifier in ctx.PartIdentifiers)
            {
                if (replaceBindings.Any(iis => Fits(iis.Item2, identifier, ctx.BindingOptions.Flags) >= 90))
                {
                    continue;
                }
                replaceBindings.AddRange(filteredBindings.Where(sb =>
                {
                    var fit = Fits(sb.binding, identifier, ctx.BindingOptions.Flags);
                    if (fit == 100)
                    {
                        return true;
                    }
                    if (fit == 90)
                    {
                        return 
                            identifier is DnsIdentifier dns && dns.Value.StartsWith('*') &&
                            !ctx.BindingOptions.Flags.HasFlag(SSLFlags.CentralSsl);
                    }
                    return false;
                }));
            }
            replaceBindings = [.. replaceBindings.Distinct()];
            return replaceBindings;
        }

        /// <summary>
        /// Choose which bindings to add for the given site
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="site"></param>
        /// <param name="bindingOptions"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private static List<(TSite, BindingOptions)> ChooseBindingsToAdd(Identifier identifier, TSite site, BindingOptions bindingOptions)
        {
            var ret = new List<(TSite, BindingOptions)>();

            // Get all http bindings which could map to the host
            var matchingBindings = site.Bindings.
                Select(x => new { binding = x, fit = Fits(x, identifier, bindingOptions.Flags) }).
                Where(x => x.fit > 0 && x.binding.Protocol == "http").
                OrderByDescending(x => x.fit).
                ToList();

            // If there are any bindings
            if (matchingBindings.Count != 0)
            {
                var bestMatch = matchingBindings.First();
                var bestMatches = matchingBindings.Where(x => x.binding.Host == bestMatch.binding.Host);
                if (bestMatch.fit == 100 || !bindingOptions.Flags.HasFlag(SSLFlags.CentralSsl))
                {
                    foreach (var match in bestMatches)
                    {
                        var addOptions = bindingOptions.WithHost(match.binding.Host);
                        // The existance of an HTTP binding with a
                        // specific IP overrules the default IP.
                        if (addOptions.IP == IISClient.DefaultBindingIp &&
                            match.binding.IP != IISClient.DefaultBindingIp &&
                            !string.IsNullOrEmpty(match.binding.IP))
                        {
                            addOptions = addOptions.WithIP(match.binding.IP);
                        }
                        ret.Add((site, addOptions));
                    }
                }
            }

            // At this point we haven't even matchedBindings a partial match for our hostname
            // so as the ultimate step we create new https binding
            if (ret.Count == 0) 
            { 
               ret.Add((site, bindingOptions));
            }
            return ret;
        }

        /// <summary>
        /// Sanity checks, prevent bad bindings from messing up IIS
        /// </summary>
        /// <param name="start"></param>
        /// <param name="match"></param>
        /// <param name="existingBindings"></param>
        /// <returns></returns>
        private bool AllowAdd(BindingOptions options, IEnumerable<IIISBinding> existingBindings)
        {
            var bindingInfoShort = $"{options.IP}:{options.Port}";
            var bindingInfoFull = $"{bindingInfoShort}:{options.Host}";

            // On Windows 2008, which does not support SNI, only one 
            // https binding can exist for each IP/port combination
            if (client.Version.Major < 8)
            {
                if (existingBindings.Any(x => x.BindingInformation.StartsWith(bindingInfoShort)))
                {
                    log.Warning($"Prevent adding duplicate binding for {bindingInfoShort}");
                    return false;
                }
            }

            // In general we shouldn't create duplicate bindings
            // because then only one of them will be usable at the
            // same time.
            if (existingBindings.Any(x => string.Equals(x.BindingInformation, bindingInfoFull, StringComparison.InvariantCultureIgnoreCase)))
            {
                log.Warning($"Prevent adding duplicate binding for {bindingInfoFull}");
                return false;
            }

            // Wildcard bindings are only supported in Windows 2016+
            if (options.Host.StartsWith("*.") && client.Version.Major < 10)
            {
                log.Warning($"Unable to create wildcard binding on this version of IIS");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Turn on SNI for #915
        /// </summary>
        /// <param name="start"></param>
        /// <param name="match"></param>
        /// <param name="allBindings"></param>
        /// <returns></returns>
        private bool UpdateExistingBindingFlags(SSLFlags start, IIISBinding match, IIISBinding[] allBindings, out SSLFlags modified)
        {
            modified = start;
            if (client.Version.Major >= 8 && !match.SSLFlags.HasFlag(SSLFlags.SNI))
            {
                if (allBindings
                    .Except([match])
                    .Where(x => x.Port == match.Port)
                    .Where(x => x.IP == match.IP)
                    .Where(x => StructuralComparisons.StructuralEqualityComparer.Equals(match.CertificateHash, x.CertificateHash))
                    .Where(x => !x.SSLFlags.HasFlag(SSLFlags.SNI))
                    .Any())
                {
                    if (!string.IsNullOrEmpty(match.Host))
                    {
                        log.Warning("Turning on SNI for existing binding to avoid conflict");
                        modified = start | SSLFlags.SNI;
                    }
                    else
                    {
                        log.Warning("Our best match was the default binding and it seems there are other non-SNI enabled " +
                            "bindings listening to the same endpoint, which means we cannot update it without potentially " +
                            "causing problems. Instead, a new binding will be created. You may manually update the bindings " +
                            "if you want IIS to be configured in a different way.");
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Make sure the flags are set correctly for updating the binding,
        /// because special conditions apply to the default binding
        /// </summary>
        /// <param name="host"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        private SSLFlags CheckFlags(bool newBinding, string host, SSLFlags flags)
        {
            // SSL flags are not supported at all by Windows 2008
            if (client.Version.Major < 8)
            {
                return SSLFlags.None;
            }

            // Add SNI on Windows Server 2012+ for new bindings
            if (newBinding &&
                !string.IsNullOrEmpty(host) &&
                client.Version.Major >= 8)
            {
                flags |= SSLFlags.SNI;
            }

            // Modern flags are not supported by IIS versions lower than 10. 
            // In fact they are not even supported by all versions of IIS 10,
            // but so far we don't know how to check for these features 
            // availability (IIS reports its version as 10.0.0 even on 
            // Server 2019).
            if (client.Version.Major < 10)
            {
                flags &= ~SSLFlags.IIS10_Flags;
            }

            // Some flags cannot be used together with the CentralSsl flag,
            // because when using CentralSsl they are supposedly configured at 
            // the server level instead of at the binding level (though the IIS 
            // Manager doesn't seem to expose these options).
            if (flags.HasFlag(SSLFlags.CentralSsl))
            {
                // Do not allow CentralSSL flag to be set on the default binding
                // Logic elsewhere in the program should prevent this 
                // from happening. This is merely a sanity check
                if (string.IsNullOrEmpty(host))
                {
                    throw new InvalidOperationException("Central SSL is not supported without a hostname");
                }
                flags &= ~SSLFlags.NotWithCentralSsl;
            }

            // All checks passed, return flags
            return flags;
        }

        /// <summary>
        /// Create a new binding
        /// </summary>
        /// <param name="site"></param>
        /// <param name="host"></param>
        /// <param name="flags"></param>
        /// <param name="thumbprint"></param>
        /// <param name="store"></param>
        /// <param name="port"></param>
        /// <param name="IP"></param>
        private IIISBinding AddBinding(TSite site, BindingOptions options)
        {
            log.Information(LogType.All, "Adding new https binding {binding}", options.Debug);
            return client.AddBinding(site, options);
        }

        /// <summary>
        /// Update an existing https binding, if needed
        /// </summary>
        /// <param name="site"></param>
        /// <param name="existingBinding"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        private BindingOptions? UpdateBinding(TSite site, TBinding existingBinding, BindingOptions options)
        {
            // Check flags
            options = options.WithFlags(CheckFlags(false, existingBinding.Host, options.Flags));

            var currentFlags = existingBinding.SSLFlags;
            if ((currentFlags & ~SSLFlags.SNI) == (options.Flags & ~SSLFlags.SNI) && // Don't care about SNI status
                ((options.Store == null && existingBinding.CertificateStoreName == null) ||
                (StructuralComparisons.StructuralEqualityComparer.Equals(existingBinding.CertificateHash, options.Thumbprint) &&
                string.Equals(existingBinding.CertificateStoreName, options.Store, StringComparison.InvariantCultureIgnoreCase))))
            {
                log.Verbose("No binding update needed");
                return null;
            }
            else
            {
                // If current binding has SNI, the updated version 
                // will also have that flag set, regardless
                // of whether or not it was requested by the caller.
                // Callers should not generally request SNI unless 
                // required for the binding, e.g. for TLS-SNI validation.
                // Otherwise let the admin be in control.

                // Update 25-12-2019: preserve all existing SSL flags
                // instead of just SNI, to accomdate the new set of flags 
                // introduced in recent versions of Windows Server.
                var preserveFlags = existingBinding.SSLFlags & ~SSLFlags.CentralSsl;
                if (options.Flags.HasFlag(SSLFlags.CentralSsl))
                {
                    preserveFlags &= ~SSLFlags.NotWithCentralSsl;
                }
                options = options.WithFlags(options.Flags | preserveFlags);
                log.Information(
                    LogType.All,
                    "Updating existing binding {binding}", 
                    options.
                        WithSiteId(site.Id).
                        WithHost(existingBinding.Host).
                        WithPort(existingBinding.Port).
                        Debug);
                client.UpdateBinding(site, existingBinding, options);
                return options;
            }
        }

        /// <summary>
        /// Test if the host fits to the binding
        /// 100: full match
        /// 90: partial match (Certificate less specific, e.g. *.example.com cert for sub.example.com binding)
        /// 50,59,48,...: partial match (IIS less specific, e.g. sub.example.com cert for *.example.com binding)
        /// 10: default match (catch-all binding)
        /// 0: no match
        /// </summary>
        /// <param name=""></param>
        /// <param name=""></param>
        /// <returns></returns>
        private static int Fits(IIISBinding binding, Identifier identifier, SSLFlags flags)
        {
            if (identifier is IpIdentifier ip)
            {
                if (string.IsNullOrEmpty(binding.Host))
                {
                    if (binding.IP == ip.Value)
                    {
                        return 100;
                    }
                    if (string.IsNullOrEmpty(binding.IP) || binding.IP == "*")
                    {
                        return 90;
                    }
                }
                return 0;
            }

            // The default (emtpy) binding matches with all hostnames.
            // But it's not supported with Central SSL
            if (string.IsNullOrEmpty(binding.Host) && (!flags.HasFlag(SSLFlags.CentralSsl)))
            {
                return 10;
            }

            // Match sub.example.com (identifier) with *.example.com (IIS)
            if (binding.Host.StartsWith("*.") && !identifier.Value.StartsWith("*."))
            {
                if (identifier.Value.ToLower().EndsWith(binding.Host.ToLower().Replace("*.", ".")))
                {
                    // If there is a binding for *.a.b.c.com (5) and one for *.c.com (3)
                    // then the hostname test.a.b.c.com (5) is a better (more specific)
                    // for the former than for the latter, so we prefer to use that.
                    var hostLevel = identifier.Value.Split('.').Length;
                    var bindingLevel = binding.Host.Split('.').Length;
                    return 50 - (hostLevel - bindingLevel);
                }
                else
                {
                    return 0;
                }
            }

            // Match *.example.com (identifier) with sub.example.com (IIS)
            if (!binding.Host.StartsWith("*.") && identifier.Value.StartsWith("*."))
            {
                if (binding.Host.ToLower().EndsWith(identifier.Value.ToLower().Replace("*.", ".")))
                {
                    // But it should not match with another.sub.example.com.
                    var hostLevel = identifier.Value.Split('.').Length;
                    var bindingLevel = binding.Host.Split('.').Length;
                    if (hostLevel == bindingLevel)
                    {
                        return 90;
                    }
                }
                else
                {
                    return 0;
                }
            }

            // Full match
            return string.Equals(binding.Host, identifier.Value, StringComparison.CurrentCultureIgnoreCase) ? 100 : 0;
        }

        /// <summary>
        /// Get all sites and their bindings
        /// </summary>
        /// <returns></returns>
        private IEnumerable<(IIISSite site, IIISBinding binding)> GetAllSites() => [.. client.Sites.SelectMany(site => site.Bindings, (site, binding) => (site, binding))];
    }
}