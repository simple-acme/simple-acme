﻿using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Runtime.Versioning;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

[assembly: SupportedOSPlatform("windows")]

namespace PKISharp.WACS.Plugins.StorePlugins
{
    [IPlugin.Plugin1<
        UserStoreOptions, UserStoreOptionsFactory,
        UserStoreCapability, UserStoreJson, UserArguments>
        ("95ee94e7-c8e2-40e6-a26f-c9fc3afa9fa5",
        Trigger, "Add to Windows Certificate Store (Current User)", 
        Name = "User Store", External = true)]
    internal class UserStore : IStorePlugin, IDisposable
    {
        internal const string Trigger = "UserStore";
        private const string DefaultStoreName = nameof(StoreName.My);
        private readonly ILogService _log;
        private readonly CertificateStoreClient _storeClient;

        public UserStore(ILogService log, ISettings settings)
        {
            _log = log;
            _storeClient = new CertificateStoreClient(DefaultStoreName, StoreLocation.CurrentUser, _log, settings);
        }

        public Task<StoreInfo?> Save(ICertificateInfo input)
        {
            _log.Information("Installing certificate in the certificate store");
            _storeClient.InstallCertificate(input, X509KeyStorageFlags.UserKeySet);
            return Task.FromResult<StoreInfo?>(new StoreInfo() {
                Name = Trigger,
                Path = DefaultStoreName
            });
        }

        public Task Delete(ICertificateInfo input)
        {
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