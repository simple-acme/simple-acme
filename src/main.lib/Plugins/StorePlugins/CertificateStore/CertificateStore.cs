using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Threading.Tasks;

using static System.IO.FileSystemAclExtensions;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    [SupportedOSPlatform("windows")]
    [IPlugin.Plugin1<
        CertificateStoreOptions, CertificateStoreOptionsFactory, 
        CertificateStoreCapability, WacsJsonPlugins, CertificateStoreArguments>
        ("e30adc8e-d756-4e16-a6f2-450f784b1a97", 
        Trigger, "Add to Windows Certificate Store (Local Computer)", 
        Name = "Windows Certificate Store")]
    internal class CertificateStore : IStorePlugin, IDisposable
    {
        internal const string Trigger = "CertificateStore";
        private const string DefaultStoreName = nameof(StoreName.My);
        private readonly ILogService _log;
        private readonly string _storeName;
        private readonly IIISClient _iisClient;
        private readonly CertificateStoreOptions _options;
        private readonly FindPrivateKey _keyFinder;
        private readonly CertificateStoreClient _storeClient;
        private readonly RunLevel _runLevel;

        public CertificateStore(
            ILogService log, IIISClient iisClient,
            ISettings settings, FindPrivateKey keyFinder, 
            CertificateStoreOptions options, RunLevel runLevel)
        {
            _log = log;
            _iisClient = iisClient;
            _options = options;
            _keyFinder = keyFinder;
            _storeName = options.StoreName ?? DefaultStore(settings, iisClient);
            if (string.Equals(_storeName, "Personal", StringComparison.InvariantCultureIgnoreCase) ||
                string.Equals(_storeName, "Computer", StringComparison.InvariantCultureIgnoreCase))
            {
                // Users trying to use the "My" store might have set "Personal" in their 
                // config files, because that's what the store is called in mmc
                _storeName = nameof(StoreName.My);
            }
            _storeClient = new CertificateStoreClient(_storeName, StoreLocation.LocalMachine, _log, settings);
            _runLevel = runLevel;
        }

        /// <summary>
        /// Determine the default certificate store
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        public static string DefaultStore(ISettings settings, IIISClient client)
        {
            // First priority: specified in settings.json 
            string? storeName;
            try
            {
                storeName = settings.Store.CertificateStore.DefaultStore;
                // Second priority: defaults
                if (string.IsNullOrWhiteSpace(storeName))
                {
                    storeName = client.Version.Major < 8 ? nameof(StoreName.My) : "WebHosting";
                }
            } 
            catch
            {
                storeName = DefaultStoreName;
            }
            return storeName;
        }

        public Task<StoreInfo?> Save(ICertificateInfo input)
        {
            _log.Information("Installing certificate in the certificate store");
            _storeClient.InstallCertificate(input, X509KeyStorageFlags.MachineKeySet);
            if (!_runLevel.HasFlag(RunLevel.Test))
            {
                _storeClient.InstallCertificateChain(input);
            }

            // Handle permissions
            SetKeyPermissions(input);

            return Task.FromResult<StoreInfo?>(new StoreInfo()
            {
                Name = Trigger,
                Path = _storeName
            });
        }

        /// <summary>
        /// Locate pivate key file and set ACLs
        /// </summary>
        /// <param name="input"></param>
        private void SetKeyPermissions(ICertificateInfo input)
        {
            // If no specific permissions are configured/requested by the user,
            // we will add full control persmissions for administrators by default.
            // Windows does this automatically on the first run (when the renewal is
            // created by one of the administators), but it will *not* do this for
            // renewals executed by the scheduled task under the SYSTEM account.
            // Adding this default ensures consistency and avoids unexpected
            // permissions issues after the first renewal period. Users not wanting
            // to grant administrators access can still configure the .renewal.json
            // to have an empty list instead of a null/undefined value.
            var full = _options.AclFullControl ?? ["administrators"];
            var read = _options.AclRead ?? [];

            // Early out, save the work if finding the key
            if (full.Count + read.Count == 0)
            {
                return;
            }

            var existing = _storeClient.FindByThumbprint(input.Thumbprint);
            if (existing == null)
            {
                _log.Error("Unable to set ACL on private key (certificate not found)");
                return;
            }
            try
            {
                var file = _keyFinder.Find(existing);
                if (file == null)
                {
                    _log.Error("Unable to set ACL on private key (file not found)");
                    return;
                }
                _log.Verbose("Private key found at {dir}", file.FullName);
                SetAcl(file, full, FileSystemRights.FullControl);
                SetAcl(file, read, FileSystemRights.Read);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Unable to set ACL on private key");
            }
        }

        private void SetAcl(FileInfo file, List<string> accounts, FileSystemRights rights)
        {
            var fs = new FileSecurity(file.FullName, AccessControlSections.All);
            foreach (var account in accounts)
            {
                try
                {
                    IdentityReference? identity = null;
                    identity = account.ToLower() switch
                    {
                        // For for international installs of Windows
                        // reference: https://learn.microsoft.com/en-US/windows-server/identity/ad-ds/manage/understand-security-identifiers
                        "administrators" => new SecurityIdentifier("S-1-5-32-544"),             
                        "network service" => new SecurityIdentifier("S-1-5-20"),
                        var s when s.StartsWith("s-1-5-") => new SecurityIdentifier(s),
                        _ => new NTAccount(account).Translate(typeof(SecurityIdentifier)),
                    };
                    fs.AddAccessRule(new FileSystemAccessRule(identity, rights, AccessControlType.Allow));
                    _log.Information("Add {rights} rights for {account}", rights, identity.Translate(typeof(NTAccount)).Value);
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "Unable to set {rights} rights for {account}", rights, account);
                }
            }
            file.SetAccessControl(fs);
        }

        /// <summary>
        /// Delete certificate from the store, but only if it is not 
        /// still active in IIS for some reason
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public Task Delete(ICertificateInfo input)
        {
            // Test if the user manually added the certificate to IIS
            if (_iisClient.HasWebSites)
            {
                var hash = input.GetHash();
                if (_iisClient.Sites.Any(site =>
                    site.Bindings.Any(binding =>
                    StructuralComparisons.StructuralEqualityComparer.Equals(binding.CertificateHash, hash) &&
                    Equals(binding.CertificateStoreName, _storeName))))
                {
                    _log.Error("The previous certificate was detected in IIS. Configure the IIS installation step to auto-update bindings.");
                    return Task.CompletedTask;
                }
            }
            _storeClient.UninstallCertificate(input.Thumbprint);
            return Task.CompletedTask;
        }

        #region IDisposable

        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _storeClient.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose() => Dispose(true);

        #endregion
    }
}