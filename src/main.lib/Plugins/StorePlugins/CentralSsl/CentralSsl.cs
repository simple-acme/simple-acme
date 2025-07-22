﻿using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    [IPlugin.Plugin1<
        CentralSslOptions, CentralSslOptionsFactory, 
        DefaultCapability, WacsJsonPlugins, CentralSslArguments>
        ("af1f77b6-4e7b-4f96-bba5-c2eeb4d0dd42",
        Trigger, "Add to IIS Central Certificate Store", 
        Name = "Central Certificate Store")]
    internal class CentralSsl : IStorePlugin
    {
        internal const string Trigger = "CentralSsl";

        private readonly ILogService _log;
        private readonly string _path;
        private readonly string? _protectionMode;

        private readonly string? _passwordRaw;
        private string? _passwordEvaluated;
        private readonly SecretServiceManager _secretService;
        private async Task<string?> GetPassword()
        {
            _passwordEvaluated ??= await _secretService.EvaluateSecret(_passwordRaw);
            return _passwordEvaluated;
        }

        public CentralSsl(
            ILogService log,
            ISettings settings,
            CentralSslOptions options,
            SecretServiceManager secretServiceManager)
        {
            _log = log;
            _passwordRaw = options.PfxPassword?.Value ?? settings.Store.CentralSsl.DefaultPassword;
            _protectionMode = settings.Store.CentralSsl?.DefaultProtectionMode;
            _secretService = secretServiceManager;

            var path = !string.IsNullOrWhiteSpace(options.Path) ?
                options.Path :
                settings.Store.CentralSsl?.DefaultPath;

            if (path != null && path.ValidPath(log))
            {
                _path = path;
                _log.Debug("Using CentralSsl path: {_path}", _path);
            }
            else
            {
                throw new Exception($"Specified CentralSsl path {path} is not valid.");
            }
        }

        private string PathForIdentifier(DnsIdentifier identifier) => Path.Combine(_path, $"{identifier.Unicode(true).Value.Replace("*", "_")}.pfx");

        public async Task<StoreInfo?> Save(ICertificateInfo input)
        {
            _log.Information("Copying certificate to the CentralSsl store");

            // Create archive with the desired settings
            if (!Enum.TryParse<PfxProtectionMode>(_protectionMode, true, out var protectionMode))
            {
                // Nothing set (pre-existing installations): stick with legacy
                protectionMode = PfxProtectionMode.Legacy;
            }
            var converted = new CertificateInfo(input, protectionMode);

            foreach (var identifier in converted.SanNames.OfType<DnsIdentifier>())
            {
                var dest = PathForIdentifier(identifier);
                _log.Information("Saving certificate to CentralSsl location {dest}", dest);
                try
                {
                    await converted.PfxSave(dest, await GetPassword());
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Error copying certificate to CentralSsl store");
                }
            }
            return new StoreInfo() {
                Name = Trigger,
                Path = _path
            };
        }

        public async Task Delete(ICertificateInfo input)
        {
            _log.Information("Removing certificate from the CentralSsl store");
            foreach (var identifier in input.SanNames.OfType<DnsIdentifier>())
            {
                var dest = PathForIdentifier(identifier);
                var fi = new FileInfo(dest);
                var cert = await LoadCertificate(fi);
                if (cert != null)
                {
                    if (string.Equals(cert.Thumbprint, input.Thumbprint, StringComparison.InvariantCultureIgnoreCase))
                    {
                        _log.Warning("Delete {fi} with thumb {thumb}", fi.FullName, cert.Thumbprint);
                        fi.Delete();
                    }
                    cert.Dispose();
                }               
            }
        }

        /// <summary>
        /// Load certificate from disk
        /// </summary>
        /// <param name="fi"></param>
        /// <returns></returns>
        private async Task<X509Certificate2?> LoadCertificate(FileInfo fi)
        {
            X509Certificate2? cert = null;
            if (!fi.Exists)
            {
                return cert;
            }
            try
            {
                cert = X509CertificateLoader.LoadPkcs12FromFile(fi.FullName, await GetPassword(), X509KeyStorageFlags.EphemeralKeySet);
            }
            catch (CryptographicException)
            {
                try
                {
                    cert = X509CertificateLoader.LoadPkcs12FromFile(fi.FullName, null, X509KeyStorageFlags.EphemeralKeySet);
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "Unable to scan certificate {name}", fi.FullName);
                }
            }
            return cert;
        }
    }
}
