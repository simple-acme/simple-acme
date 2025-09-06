using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.StorePlugins;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.InstallationPlugins
{
    [IPlugin.Plugin1<
        IISOptions, IISOptionsFactory, 
        IISCapability, WacsJsonPlugins, IISArguments>
        (ID, Trigger, "Create or update bindings in IIS", Name = "Manage IIS bindings")]
    [IPlugin.Plugin<
        IISFtpOptions, IISFTPOptionsFactory,
        IISCapability, WacsJsonPlugins>
        ("13058a79-5084-48af-b047-634e0ee222f4",
        "IISFTP", "Create or update FTP bindings in IIS", Hidden = true)]
    internal class IIS(IISOptions options, IIISClient iisClient, ISettings settings, ILogService log, Target target) : IInstallationPlugin
    {
        internal const string Trigger = "IIS";
        internal const string ID = "ea6a5be3-f8de-4d27-a6bd-750b619b2ee2";

        Task<bool> IInstallationPlugin.Install(
            Dictionary<Type, StoreInfo> storeInfo,
            ICertificateInfo newCertificate,
            ICertificateInfo? oldCertificate)
        {
            // Store validation
            var centralSslForHttp = false;
            var centralSsl = storeInfo.ContainsKey(typeof(CentralSsl));
            var certificateStore = storeInfo.ContainsKey(typeof(CertificateStore));
            var certificateStoreName = (string?)null;
            if (certificateStore)
            {
                certificateStoreName = storeInfo[typeof(CertificateStore)].Path;
            }
            if (!centralSsl && !certificateStore)
            {
                // No supported store
                var errorMessage = "The IIS installation plugin requires the CertificateStore and/or CentralSsl store plugin";
                log.Error(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }

            // Determine installation site which is used
            // to create new bindings if needed. This may
            // be an FTP site or a web site
            var installationSite = default(IIISSite);
            if (options.SiteId != null)
            {
                try 
                {
                    installationSite = iisClient.GetSite(options.SiteId.Value);
                }
                catch (Exception ex)
                {
                    // Site may have been stopped or removed
                    // after initial renewal setup. This means
                    // we don't know where to create new bindings
                    // anymore, but that's not a fatal error.
                    log.Warning(ex, "Installation site {id} not found running in IIS, only existing bindings will be updated", options.SiteId);
                }
            }
            foreach (var part in target.Parts)
            {
                // Use source plugin provided ID
                // with override by installation site ID (for non-IIS source)
                // for missing site the value might stay null, which means
                // only pre-existing bindings will be updated an no new
                // bindings can be created.
                part.SiteId ??= installationSite?.Id;

                // Use source plugin provided type
                // with override by installation site type (for non-IIS source)
                // with override by plugin variant (for missing installation sites)
                part.SiteType ??= installationSite?.Type ?? (options is IISFtpOptions ? IISSiteType.Ftp : IISSiteType.Web);
            }

            if (centralSsl)
            {
                centralSslForHttp = true;
                var supported = true;
                var reason = "";
                if (iisClient.Version.Major < 8)
                {
                    reason = "CentralSsl store requires IIS version 8.0 or higher";
                    supported = false;
                    centralSslForHttp = false;
                }
                if (target.Parts.Any(p => p.SiteType == IISSiteType.Ftp)) 
                {
                    reason = "CentralSsl store is not supported for FTP sites";
                    supported = false;
                }
                if (!supported && !certificateStore)
                {
                    // Only throw error if there is no fallback 
                    // available to the CertificateStore plugin.
                    log.Error(reason);
                    throw new InvalidOperationException(reason);
                } 
            }

            var settingsFlags = (settings.Installation.IIS?.BindingFlags ?? SSLFlags.None) & SSLFlags.OptionalFlags;

            foreach (var part in target.Parts)
            {
                var httpIdentifiers = part.Identifiers.OfType<DnsIdentifier>();
                var bindingOptions = new BindingOptions().WithFlags(settingsFlags);

                // Pick between CentralSsl and CertificateStore
                bindingOptions = centralSslForHttp
                    ? bindingOptions.
                        WithFlags(SSLFlags.CentralCertStore | bindingOptions.Flags)
                    : bindingOptions.
                        WithThumbprint(newCertificate.GetHash()).
                        WithStore(certificateStoreName);

                switch (part.SiteType)
                {
                    case IISSiteType.Web:
                        // Optionaly overrule the standard IP for new bindings 
                        if (!string.IsNullOrEmpty(options.NewBindingIp))
                        {
                            bindingOptions = bindingOptions.
                                WithIP(options.NewBindingIp);
                        }
                        // Optionaly overrule the standard port for new bindings 
                        if (options.NewBindingPort > 0)
                        {
                            bindingOptions = bindingOptions.
                                WithPort(options.NewBindingPort.Value);
                        }
                        if (part.SiteId != null)
                        {
                            bindingOptions = bindingOptions.
                                WithSiteId(part.SiteId.Value);
                        }
                        iisClient.UpdateHttpSite(httpIdentifiers, bindingOptions, oldCertificate?.GetHash(), newCertificate.SanNames);
                        if (certificateStore) 
                        {
                            iisClient.UpdateFtpSite(0, certificateStoreName, newCertificate, oldCertificate);
                        }
                        break;
                    case IISSiteType.Ftp:
                        // Update FTP site
                        iisClient.UpdateFtpSite(part.SiteId!.Value, certificateStoreName, newCertificate, oldCertificate);
                        iisClient.UpdateHttpSite(httpIdentifiers, bindingOptions, oldCertificate?.GetHash(), newCertificate.SanNames);
                        break;
                    default:
                        log.Error("Unknown site type");
                        break;
                }
            }

            return Task.FromResult(true);
        }
    }
}
