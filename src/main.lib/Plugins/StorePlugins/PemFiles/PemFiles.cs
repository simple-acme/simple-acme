﻿using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    [IPlugin.Plugin1<
        PemFilesOptions, PemFilesOptionsFactory, 
        DefaultCapability, WacsJsonPlugins, PemFilesArguments>
        ("e57c70e4-cd60-4ba6-80f6-a41703e21031",
        Trigger, "Create PEM encoded files (for Apache, nginx, etc.)", 
        Name = "PEM files")]
    internal class PemFiles : IStorePlugin
    {
        internal const string Trigger = "PemFiles";

        private readonly ILogService _log;
        private readonly string _path;
        private readonly string? _name;

        private readonly string? _passwordRaw;
        private string? _passwordEvaluated;
        private readonly SecretServiceManager _secretService;
        private async Task<string?> GetPassword()
        {
            _passwordEvaluated ??= await _secretService.EvaluateSecret(_passwordRaw);
            return _passwordEvaluated;
        }

        public PemFiles(
            ILogService log,
            ISettingsService settings,
            PemFilesOptions options, 
            SecretServiceManager secretServiceManager)
        {
            _log = log;

            _passwordRaw = options.PemPassword?.Value ?? settings.Store.PemFiles.DefaultPassword;
            _secretService = secretServiceManager;
            _name = options.FileName;
            var path = options.Path;
            if (string.IsNullOrWhiteSpace(path))
            {
                path = settings.Store.PemFiles.DefaultPath;
            }
            if (!string.IsNullOrWhiteSpace(path) && path.ValidPath(log))
            {
                _log.Debug("Using .pem files path: {path}", path);
                _path = path;
            }
            else
            {
                throw new Exception($"Specified .pem files path {path} is not valid.");
            }
        }

        public async Task<StoreInfo?> Save(ICertificateInfo input)
        {
            
            _log.Information("Exporting .pem files to {folder}", _path);
            try
            {
                // Determine name
                var name = _name ?? input.CommonName?.Value ?? input.SanNames.First().Value;
                name = name.Replace("*", "_");

                // Base certificate
                var certificateExport = input.Certificate.GetEncoded();
                var certString = PemService.GetPem("CERTIFICATE", certificateExport);
                var chainString = "";
                await FileInfoExtensions.SafeWrite(Path.Combine(_path, $"{name}-crt.pem"), certString);

                // Rest of the chain
                foreach (var chainCertificate in input.Chain)
                {
                    // Do not include self-signed certificates, root certificates
                    // are supposed to be known already by the client.
                    if (chainCertificate.SubjectDN.ToString() != chainCertificate.IssuerDN.ToString())
                    {
                        var chainCertificateExport = chainCertificate.GetEncoded();
                        chainString += PemService.GetPem("CERTIFICATE", chainCertificateExport);
                    }
                }

                // Save complete chain
                await FileInfoExtensions.SafeWrite(Path.Combine(_path, $"{name}-chain.pem"), certString + chainString);
                await FileInfoExtensions.SafeWrite(Path.Combine(_path, $"{name}-chain-only.pem"), chainString);

                // Private key
                if (input.PrivateKey != null)
                {
                    var pkPem = PemService.GetPem(input.PrivateKey, await GetPassword());
                    if (!string.IsNullOrEmpty(pkPem))
                    {
                        await FileInfoExtensions.SafeWrite(Path.Combine(_path, $"{name}-key.pem"), pkPem);
                    }
                } 
                else
                {
                    _log.Warning("No private key found in cache");
                }
                return new StoreInfo() {
                    Name = Trigger,
                    Path = _path
                };
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error exporting .pem files to folder");
                return null;
            }
        }

        public Task Delete(ICertificateInfo input) => Task.CompletedTask;
    }
}
