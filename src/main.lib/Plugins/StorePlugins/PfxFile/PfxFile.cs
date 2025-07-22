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
        PfxFileOptions, PfxFileOptionsFactory, 
        DefaultCapability, WacsJsonPlugins, PfxFileArguments>
        ("2a2c576f-7637-4ade-b8db-e8613b0bb33e",
        Trigger, "Create PFX/PKCS12 archive file", 
        Name = "PFX file")]
    internal class PfxFile : IStorePlugin
    {
        internal const string Trigger = "PfxFile";

        private readonly ILogService _log;
        private readonly string _path;
        private readonly string? _name;
        private readonly string? _protectionMode;

        private readonly string? _passwordRaw;
        private string? _passwordEvaluated;
        private readonly SecretServiceManager _secretService;
        private async Task<string?> GetPassword()
        {
            _passwordEvaluated ??= await _secretService.EvaluateSecret(_passwordRaw);
            return _passwordEvaluated;
        }

        public static string? DefaultPath(ISettings settings) => 
            settings.Store.PfxFile?.DefaultPath;

        public static string? DefaultPassword(ISettings settings)
            => settings.Store.PfxFile?.DefaultPassword;

        public PfxFile(
            ILogService log, 
            ISettings settings, 
            PfxFileOptions options,
            SecretServiceManager secretServiceManager)
        {
            _log = log;

            _passwordRaw = options.PfxPassword?.Value ?? settings.Store.PfxFile?.DefaultPassword;
            _secretService = secretServiceManager;
            _name = options.FileName;
            _protectionMode = settings.Store.PfxFile?.DefaultProtectionMode;

            var path = !string.IsNullOrWhiteSpace(options.Path) ? 
                options.Path :
                settings.Store.PfxFile?.DefaultPath;

            if (path != null && path.ValidPath(log))
            {
                _path = path;
                _log.Debug("Using pfx file path: {_path}", _path);
            }
            else
            {
                throw new Exception($"Specified pfx file path {path} is not valid.");
            }
        }

        private string PathForIdentifier(string identifier) => Path.Combine(_path, $"{identifier.Replace("*", "_")}.pfx");

        /// <summary>
        /// We don't save the certificate immediately for two reasons:
        /// 1. We want to change the PK alias tto something more predictable
        ///    because the normal one changes with every renewal due to the
        ///    date being part of the friendly name
        /// 2. We want the user to be able to select the PfxProtectionMode
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<StoreInfo?> Save(ICertificateInfo input)
        {
            try
            {
                // Create archive with the desired settings
                if (!Enum.TryParse<PfxProtectionMode>(_protectionMode, true, out var protectionMode))
                {
                    // Nothing set (pre-existing installations): stick with legacy
                    protectionMode = PfxProtectionMode.Legacy;
                }
                var output = PfxService.GetPfx(protectionMode);

                // Copy all key and cert entries to the new archive
                var outBc = output.Store;
                var inBc = input.Collection.Store;
                var aliases = inBc.Aliases.ToList();
                var keyAlias = aliases.FirstOrDefault(inBc.IsKeyEntry);
                if (keyAlias != null)
                {
                    outBc.SetKeyEntry(
                        input.CommonName?.Value ?? input.SanNames.First().Value,
                        inBc.GetKey(keyAlias),
                        inBc.GetCertificateChain(keyAlias));
                }
                else
                {
                    foreach (var alias in aliases)
                    {
                        outBc.SetCertificateEntry(alias, inBc.GetCertificate(alias));
                    }
                }

                // Save to disk
                var dest = PathForIdentifier(_name ?? input.CommonName?.Value ?? input.SanNames.First().Value);
                var outInfo = new CertificateInfo(output);
                _log.Information("Copying certificate to the pfx folder {dest}", dest);
                await outInfo.PfxSave(dest, await GetPassword());
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Error copying certificate to pfx path");
            }
            return new StoreInfo() {
                Name = Trigger,
                Path = _path
            };
        }

        public Task Delete(ICertificateInfo input) => Task.CompletedTask;
    }
}
