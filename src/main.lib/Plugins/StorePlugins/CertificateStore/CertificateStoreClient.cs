using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    [SupportedOSPlatform("windows")]
    public class CertificateStoreClient(string storeName, StoreLocation storeLocation, ILogService log, ISettings settings) : IDisposable
    {
        private readonly X509Store _store = new(storeName, storeLocation);
        private bool disposedValue;

        public X509Certificate2? FindByThumbprint(string thumbprint) => GetCertificate(x => string.Equals(x.Thumbprint, thumbprint));

        /// <summary>
        /// Install the main certs with potential private key
        /// </summary>
        /// <param name="certificate"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        public bool InstallCertificate(ICertificateInfo certificate, X509KeyStorageFlags flags)
        {
            // Determine storage flags
#pragma warning disable CS0618 // Type or member is obsolete
            var exportable =
                settings.Store.CertificateStore.PrivateKeyExportable == true ||
                (settings.Store.CertificateStore.PrivateKeyExportable == null && settings.Security.PrivateKeyExportable == true);
#pragma warning restore CS0618 // Type or member is obsolete
            if (exportable)
            {
                flags |= X509KeyStorageFlags.Exportable;
            }
            flags |= X509KeyStorageFlags.PersistKeySet;
            var password = PasswordGenerator.Generate();
            var success = false;
            var attemptConvert = settings.Store.CertificateStore.UseNextGenerationCryptoApi != true;
            if (attemptConvert)
            {
                success = SaveWithRetry(certificate, (input) => ConvertAndSave(input, flags, password));
                if (!success)
                {
                    log.Warning("Unable to save using CryptoAPI, retrying with CNG...");
                }
            }
            if (!success)
            {
                SaveWithRetry(certificate, (input) => { RegularSave(input, flags, password); return true; });
            }
            return exportable;
        }

        /// <summary>
        /// Retry with fallback to legacy protection mode
        /// </summary>
        /// <param name="original"></param>
        /// <param name="execute"></param>
        /// <returns></returns>
        public bool SaveWithRetry(ICertificateInfo original, Func<ICertificateInfo, bool> execute)
        {
            try
            {
                return execute(original);
            }
            catch (CryptographicException)
            {
                if (original.Collection.ProtectionMode != PfxProtectionMode.Legacy)
                {
                    // Retry with legacy PFX protection instead of modern one
                    log.Warning("Unable to save using PfxProtectionMode {chosen}, retrying with {legacy}...", original.Collection.ProtectionMode, PfxProtectionMode.Legacy);
                    var legacy = new CertificateInfo(original, PfxProtectionMode.Legacy);
                    return execute(legacy);
                }
                else
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Try to convert the certificate before storing it
        /// </summary>
        /// <param name="certificate"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        public bool ConvertAndSave(ICertificateInfo certificate, X509KeyStorageFlags flags, string? password = null)
        {
            var dotnet = default(X509Certificate2);

            // We should always be exportable before we can try 
            // conversion to the legacy Crypto API. Look for the
            // certificate with the private key attached.
            var tempFlags = X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable;

            try
            {
                var collection = certificate.AsCollection(tempFlags, log, password);
                dotnet = collection.OfType<X509Certificate2>().FirstOrDefault(x => x.HasPrivateKey);
            }
            catch (Exception ex)
            {
                log.Verbose("{ex}", ex);
                return false;
            }
            if (dotnet == null)
            {
                // No certificate with private key found
                // so we can save it the old fashioned 
                // way without conversion.
                return false;
            }
            try
            {
                dotnet = ConvertCertificate(dotnet, flags, password);
            }
            catch (Exception ex)
            {
                log.Verbose("{ex}", ex);
                return false;
            }
            if (dotnet == null)
            {
                // Certificate is not RSA 
                return false;
            }
            try
            {
                SaveToStore(_store, dotnet, true);
            }
            catch (Exception ex)
            {
                log.Verbose("{ex}", ex);
                return false;
            }

            // Success
            return true;
        }

        /// <summary>
        /// Save for CNG scenario (modern crypto). Fallback for failing converstion or
        /// for users that've requested only using this through settings.json
        /// </summary>
        /// <param name="certificate"></param>
        /// <param name="flags"></param>
        public void RegularSave(ICertificateInfo certificate, X509KeyStorageFlags flags, string? password = null)
        {
            // Tripwire code: this will fail when the certificate is 
            // using a key protection mechasims that is not supported
            // by the operating system, causing the caller to retry
            // using a different protection
            var collection = certificate.
                AsCollection(X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable, log, password).
                OfType<X509Certificate2>().
                ToList();
            
            // If we don't trip on the above, repeat the same 
            // with the actual flags that we want to use.
            collection = certificate.AsCollection(flags, log, password).OfType<X509Certificate2>().ToList();
            var dotnet = collection.FirstOrDefault(x => x.HasPrivateKey);
            if (dotnet == null)
            {
                // If conversion failed or was not attempted, use original set of flags
                // but here we should consider the scenario that the private key is not 
                // present at all.
                dotnet ??= collection.FirstOrDefault(x => string.Equals(x.Thumbprint, certificate.Thumbprint, StringComparison.InvariantCultureIgnoreCase));
            }
            if (dotnet == null)
            {
                // When no match by thumbprint, try matching up issuers
                dotnet ??= collection.FirstOrDefault(x => !collection.Any(y => x.Subject == y.Issuer));
            }
            if (dotnet == null)
            {
                throw new Exception("Unable to select leaf certificate");
            }
            SaveToStore(_store, dotnet, true);
        }

        /// <summary>
        /// Actual save to store
        /// </summary>
        /// <param name="store"></param>
        /// <param name="dotnet"></param>
        private void SaveToStore(X509Store store, X509Certificate2 dotnet, bool overwrite)
        {
            var close = false;
            if (!store.IsOpen)
            {
                log.Debug("Open store {name}", store.Name);
                store.Open(OpenFlags.ReadWrite);
                close = true;
            }
            var found = store.Certificates.Find(X509FindType.FindByThumbprint, dotnet.Thumbprint, false).Count > 0;
            if (!found || overwrite)
            {
                var label = dotnet.FriendlyName;
                if (string.IsNullOrWhiteSpace(label))
                {
                    label = dotnet.Subject;
                }
                log.Information(LogType.All, "{verb} certificate {FriendlyName} in store {name}", found ? "Replacing" : "Adding", label, store.Name);
                log.Verbose("{sub}/{iss} ({thumb})", dotnet.Subject, dotnet.Issuer, dotnet.Thumbprint);
                store.Add(dotnet);
            }
            else
            {
                log.Verbose("{sub} - {iss} ({thumb}) already exists in {store}", dotnet.Subject, dotnet.Issuer, dotnet.Thumbprint, store.Name);
            }
            if (close)
            {
                log.Debug("Close store {name}", store.Name);
                store.Close();
            }
            // Release native resources
            dotnet.Dispose();
        }

        /// <summary>
        /// Install the chain certs
        /// </summary>
        /// <param name="certificate"></param>
        public void InstallCertificateChain(ICertificateInfo certificate)
        {
            using var imStore = new X509Store(StoreName.CertificateAuthority, storeLocation);
            var store = imStore;
            try
            {
                imStore.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadWrite);
                if (!imStore.IsOpen)
                {
                    throw new Exception("Store failed to open");
                }
            }
            catch (Exception ex)
            {
                log.Warning(ex, $"Error opening intermediate certificate store");
                store = _store;
            }
            foreach (var bcCert in certificate.Chain)
            {
                var cert = X509CertificateLoader.LoadCertificate(bcCert.GetEncoded());
                try
                {
                    SaveToStore(store, cert, false);
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "Error saving intermediate certificate");
                }
            }
            store.Close();
        }

        /// <summary>
        /// Remove superfluous certificate from the store
        /// </summary>
        /// <param name="thumbprint"></param>
        public void UninstallCertificate(string thumbprint)
        {
            log.Information("Uninstalling certificate from the certificate store");
            try
            {
                _store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadWrite);
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error encountered while opening certificate store");
                throw;
            }
            log.Debug("Opened certificate store {Name}", _store.Name);
            try
            {
                var col = _store.Certificates;
                foreach (var cert in col)
                {
                    if (string.Equals(cert.Thumbprint, thumbprint, StringComparison.InvariantCultureIgnoreCase))
                    {
                        log.Information(LogType.All, "Removing certificate {cert} from store {name}", cert.FriendlyName, _store.Name);
                        _store.Remove(cert);
                    }
                }
                log.Debug("Closing certificate store");
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error removing certificate");
                throw;
            }
            _store.Close();
        }

        /// <summary>
        /// Set the right flags on the certificate and
        /// convert the private key to the right cryptographic
        /// provider
        /// </summary>
        /// <param name="original"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        private X509Certificate2? ConvertCertificate(X509Certificate2 original, X509KeyStorageFlags flags, string? password = null)
        {
            // If there is an RSA key, we change it to be stored in the
            // Microsoft RSA SChannel Cryptographic Provider so that its 
            // usable for older versions of Microsoft Exchange and exportable
            // from IIS. This also is required to allow the SetAcl logic to 
            // work.
            using var rsaPrivateKey = original.GetRSAPrivateKey();
            if (rsaPrivateKey == null)
            {
                log.Verbose("No RSA private key detected");
                return null;
            }
            else
            {
                log.Debug("Converting private key...");
            }

            // Export private key parameters
            // https://github.com/dotnet/runtime/issues/36899
            using var tempRsa = RSA.Create();
            var pbeParameters = new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 10);
            tempRsa.ImportEncryptedPkcs8PrivateKey(password, rsaPrivateKey.ExportEncryptedPkcs8PrivateKey(password, pbeParameters), out var read);

            var cspFlags = CspProviderFlags.NoPrompt;
            if (flags.HasFlag(X509KeyStorageFlags.MachineKeySet))
            {
                cspFlags |= CspProviderFlags.UseMachineKeyStore;
            }
            if (!flags.HasFlag(X509KeyStorageFlags.Exportable))
            {
                cspFlags |= CspProviderFlags.UseNonExportableKey;
            }
            // Documentation for CspParameters:
            // https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.cspparameters?view=net-9.0
            // Enumeration of possible provider types: 
            // HKEY_LOCAL_MACHINE\Software\Microsoft\Cryptography\Defaults\Provider Types
            var cspParameters = new CspParameters
            {
                KeyContainerName = Guid.NewGuid().ToString(),
                KeyNumber = 1,
                Flags = cspFlags,
                ProviderType = 12 // Microsoft RSA SChannel Cryptographic Provider
            };
            var rsaProvider = new RSACryptoServiceProvider(cspParameters);
            var parameters = tempRsa.ExportParameters(true);
            rsaProvider.ImportParameters(parameters);

            var tempPfx = X509CertificateLoader.LoadCertificate(original.Export(X509ContentType.Cert));
            tempPfx = tempPfx.CopyWithPrivateKey(rsaProvider);
            tempPfx.FriendlyName = original.FriendlyName;
            return tempPfx;
        }

        /// <summary>
        /// Find certificate in the store
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public X509Certificate2? GetCertificate(Func<X509Certificate2, bool> filter)
        {
            var possibles = new List<X509Certificate2>();
            try
            {
                _store.Open(OpenFlags.OpenExistingOnly | OpenFlags.ReadOnly);
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error encountered while opening certificate store");
                return null;
            }
            try
            {
                var col = _store.Certificates;
                foreach (var cert in col)
                {
                    if (filter(cert))
                    {
                        possibles.Add(cert);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error finding certificate in certificate store");
                return null;
            }
            _store.Close();
            return possibles.OrderByDescending(x => x.NotBefore).FirstOrDefault();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _store.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
