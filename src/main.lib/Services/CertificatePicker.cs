using PKISharp.WACS.Clients.Acme;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Services
{
    internal class CertificatePicker(
        ILogService log,
        ISettingsService settingsService)
    {

        /// <summary>
        /// Get the name for the root issuer
        /// </summary>
        /// <param name="option"></param>
        /// <returns></returns>
        private static string? Root(CertificateOption option) => 
            option.WithoutPrivateKey.Chain.LastOrDefault()?.IssuerDN.CommonName(true) ?? 
            option.WithoutPrivateKey.Certificate.IssuerDN.CommonName(true);

        /// <summary>
        /// Choose between different versions of the certificate
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public CertificateOption Select(List<CertificateOption> options)
        {
            var selected = options[0];
            if (options.Count > 1)
            {
                log.Debug("Found {n} version(s) of the certificate", options.Count);
                foreach (var option in options)
                {
                    log.Debug("Option {n} issued by {issuer} (thumb: {thumb})", 
                        options.IndexOf(option) + 1, 
                        Root(option), 
                        option.WithPrivateKey.Thumbprint);
                }
                if (!string.IsNullOrEmpty(settingsService.Acme.PreferredIssuer))
                {
                    var match = options.FirstOrDefault(x => string.Equals(Root(x), settingsService.Acme.PreferredIssuer, StringComparison.InvariantCultureIgnoreCase));
                    if (match != null)
                    {
                        selected = match;
                    }
                }
                log.Debug("Selected option {n}", options.IndexOf(selected) + 1);
            }
            if (!string.IsNullOrEmpty(settingsService.Acme.PreferredIssuer) &&
                !string.Equals(Root(selected), settingsService.Acme.PreferredIssuer, StringComparison.InvariantCultureIgnoreCase))
            {
                log.Warning("Unable to find certificate issued by preferred issuer {issuer}", settingsService.Acme.PreferredIssuer);
            }
            return selected;
        }
    }
}