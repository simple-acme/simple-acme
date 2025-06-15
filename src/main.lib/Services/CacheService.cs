﻿using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    /// <summary>
    /// Stores CSR, KEY and PFX files on disk for future reference
    /// by the program itself, e.g. to determine if there are changes 
    /// to the shape of the certificate, renewal is due or revokation
    /// </summary>
    internal class CacheService : ICacheService
    {
        private const string KeysPostfix = ".keys";
        private const string CsrPostFix = "-csr.pem";
        private const string PfxPostFix = "-temp.pfx";
        private const string PfxPostFixLegacy = "-cache.pfx";

        private readonly ILogService _log;
        private readonly ISettings _settings;
        private readonly DirectoryInfo _cache;

        public CacheService(ILogService log, ISettings settingsService)
        {
            _settings = settingsService;
            _log = log;
            _cache = new DirectoryInfo(settingsService.Cache.CachePath);
            if (settingsService.Valid)
            {
                CheckStaleFiles();
            }
        }

        /// <summary>
        /// List all files older than 120 days from the certificate
        /// cache, because that means that the certificates have been
        /// expired for 30 days. User might want to clean them up
        /// </summary>
        private void CheckStaleFiles()
        {
            var days = Math.Max(
                _settings.Cache.DeleteStaleFilesDays, 
                _settings.ScheduledTask.RenewalDays + 30);
            var files = _cache.
                GetFiles().
                Where(x => x.LastWriteTime < DateTime.Now.AddDays(-days));
            var count = files.Count();
            if (count > 0)
            {
                if (_settings.Cache.DeleteStaleFiles)
                {
                    _log.Verbose("Deleting stale cache files...");
                    try
                    {
                        foreach (var file in files)
                        {
                            file.Delete();
                        }
                        _log.Information("Deleted {nr} files older than {days} days", count, days);
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, "Error deleting stale files");
                    }
                }
                else
                {
                    _log.Warning("Found {nr} files older than {days} days in {cachePath}, " +
                        "enable Cache.DeleteStaleFiles in settings.json to automatically " +
                        "delete these on each run.", count, days, _cache.FullName);
                }
            }
        }

        /// <summary>
        /// Delete cached files related to a specific renewal
        /// </summary>
        /// <param name="renewal"></param>
        private void ClearCache(string pattern)
        {
            foreach (var f in _cache.EnumerateFiles(pattern))
            {
                _log.Verbose("Deleting {file} from {folder}", f.Name, _cache.FullName);
                try
                {
                    f.Delete();
                }
                catch (Exception ex)
                {
                    _log.Warning(ex, "Error deleting {file} from {folder}", f.Name, _cache.FullName);
                }
            }
        }

        /// <summary>
        /// Delete cached files related to a specific order
        /// </summary>
        /// <param name="renewal"></param>
        private void ClearCache(Order order, string postfix = "*") =>
            ClearCache($"{order.Renewal.Id}-{order.CacheKeyPart}-{postfix}");

        /// <summary>
        /// Delete all files related to the renewal
        /// </summary>
        /// <param name="renewal"></param>
        void ICacheService.Delete(Renewal renewal) =>
            ClearCache($"{renewal.Id}*");

        /// <summary>
        /// Delete all files related to an order in the renewal
        /// </summary>
        /// <param name="renewal"></param>
        /// <param name="order"></param>
        void ICacheService.Delete(Renewal renewal, string order) =>
            ClearCache($"{renewal.Id}-{order}-*");

        /// <summary>
        /// Called on revoke to delete the private key,
        /// so that it's regenerated even when the --reuse-privatekey
        /// parameter is used.
        /// </summary>
        /// <param name="renewal"></param>
        void ICacheService.Revoke(Renewal renewal)
        {
            ClearCache($"{renewal.Id}{KeysPostfix}");
            ClearCache($"{renewal.Id}-*{KeysPostfix}");
        }

        /// <summary>
        /// Encrypt or decrypt the cached private keys
        /// </summary>
        public async Task Encrypt()
        {
            foreach (var f in _cache.EnumerateFiles($"*{KeysPostfix}"))
            {
                var x = new ProtectedString(File.ReadAllText(f.FullName), _log);
                _log.Information("Rewriting {x}", f.Name);
                await f.SafeWrite(x.DiskValue(_settings.Security.EncryptConfig));
            }
        }

        /// <summary>
        /// Find local certificate file based on naming conventions
        /// </summary>
        /// <param name="renewal"></param>
        /// <param name="postfix"></param>
        /// <param name="prefix"></param>
        /// <returns></returns>
        private string GetPath(Renewal renewal, string postfix, string prefix = "") =>
            Path.Combine(_cache.FullName, $"{prefix}{renewal.Id}{postfix}");

        /// <summary>
        /// Read from the disk cache, only returns exact match
        /// (i.e. the certificate could be used right away).
        /// </summary>
        /// <param name="renewal"></param>
        /// <returns></returns>
        public CertificateInfoCache? CachedInfo(Order order)
        {
            _log.Debug("Reading certificate cache");
            var cachedInfos = CachedInfos(order.Renewal);
            if (!cachedInfos.Any())
            {
                _log.Debug("No cache files found for renewal");
                return null;
            }

            var cacheVersion = MaxCacheKeyVersion;
            var fileCache = default(CertificateInfoCache);
            while (fileCache == null && cacheVersion > 0)
            {
                var fileName = GetPath(order.Renewal, $"-{CacheKey(order, cacheVersion)}{PfxPostFix}");
                fileCache = cachedInfos.Where(x => x.CacheFile.FullName == fileName).FirstOrDefault();
                if (fileCache == null)
                {
                    _log.Verbose("v{current} cache key not found, fall back to v{next}", cacheVersion, cacheVersion - 1);
                }
                cacheVersion--;
            }
            if (fileCache == null)
            {
                var legacyFile = GetPath(order.Renewal, PfxPostFixLegacy);
                var candidate = cachedInfos.Where(x => x.CacheFile.FullName == legacyFile).FirstOrDefault();
                if (candidate != null)
                {
                    if (Match(candidate, order.Target))
                    {
                        fileCache = candidate;
                    }
                    else
                    {
                        _log.Verbose("v0 cache found but not matched");
                    }
                }
                else
                {
                    _log.Debug("No cached certificate could be found");
                }
            }
            return fileCache;
        }

        /// <summary>
        /// All cached files available for a specific renewal, which
        /// may include multiple orders
        /// </summary>
        /// <param name="renewal"></param>
        /// <returns></returns>
        public IEnumerable<CertificateInfoCache> CachedInfos(Renewal renewal)
        {
            var ret = new List<CertificateInfoCache>();
            var nameAll = GetPath(renewal, "*.pfx");
            var directory = new DirectoryInfo(Path.GetDirectoryName(nameAll)!);
            var allPattern = Path.GetFileName(nameAll);
            var allFiles = directory.EnumerateFiles(allPattern + "*");
            var fileCache = allFiles.OrderByDescending(x => x.LastWriteTime);
            foreach (var file in fileCache)
            {
                try
                {
                    ret.Add(FromCache(file, renewal.PfxPassword?.Value));
                }
                catch (Exception ex)
                {
                    // File corrupt or invalid password?
                    _log.Warning(ex, "Unable to read {i} from certificate cache", file.Name);
                }
            }
            return ret;
        }

        /// <summary>
        /// See if the information in the certificate matches
        /// that of the specified target. Used to figure out whether
        /// or not the cache is out of date.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        private static bool Match(ICertificateInfo info, Target target)
        {
            var identifiers = target.GetIdentifiers(false);
            return info.CommonName == target.CommonName?.Unicode(false) &&
                info.SanNames.Count() == identifiers.Count &&
                info.SanNames.All(h => identifiers.Contains(h.Unicode(false)));
        }

        /// <summary>
        /// Latest version of the cache key generation algorithm
        /// to make sure that future releases don't invalidate 
        /// the entire cache on upgrades.
        /// </summary>
        private const int MaxCacheKeyVersion = 4;

        /// <summary>
        /// To check if it's possible to reuse a previously retrieved
        /// certificate we create a hash of its key properties and included
        /// that hash in the file name. If we get the same hash on a 
        /// subsequent run, it means it's safe to reuse (no relevant changes).
        /// </summary>
        /// <param name="renewal"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        private static string CacheKey(Order order, int version = MaxCacheKeyVersion)
        {
            // Check if we can reuse a cached certificate and/or order
            // based on currently active set of parameters and shape of 
            // the target.
            var cacheKeyBuilder = new StringBuilder();
            cacheKeyBuilder.Append(order.CacheKeyPart);
            _ = version > 1 ?
                cacheKeyBuilder.Append(order.Target.CommonName?.Value) :
                cacheKeyBuilder.Append(order.Target.CommonName);
            cacheKeyBuilder.Append(string.Join(',', order.Target.GetIdentifiers(true).OrderBy(x => x).Select(x => x.Value.ToLower())));
            _ = order.Target.UserCsrBytes != null ?
                cacheKeyBuilder.Append(Convert.ToBase64String(order.Target.UserCsrBytes.ToArray())) :
                cacheKeyBuilder.Append('-');
            _ = order.Renewal.CsrPluginOptions != null ?
                cacheKeyBuilder.Append(JsonSerializer.Serialize(order.Renewal.CsrPluginOptions, WacsJson.Default.CsrPluginOptions)) :
                cacheKeyBuilder.Append('-');
            if (version > 3)
            {
                // Make SiteId part of the cache key so that we will 
                // detect moved bindings and re-run the installation
                // step accordingly.
                cacheKeyBuilder.Append(string.Join(',', order.Target.Parts.Select(p => p.SiteId ?? 0)));
            }
            var key = cacheKeyBuilder.ToString().SHA1();
            if (version > 2)
            {
                key = $"{order.CacheKeyPart ?? "main"}-{key}";
            }
            return key;
        }

        /// <summary>
        /// Cache loading the .pfx file from disk and parsing the certificate
        /// this is a serious performance win in cases were lots of certificates
        /// are create from a single order.
        /// </summary>
        /// <param name="pfxFileInfo"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private CertificateInfoCache FromCache(FileInfo pfxFileInfo, string? password)
        {
            var key = pfxFileInfo.FullName;
            if (_infoCache.TryGetValue(key, out var value))
            {
                if (value.CacheFile.LastWriteTime == pfxFileInfo.LastWriteTime)
                {
                    return value;
                }
                else
                {
                    _infoCache[key] = new CertificateInfoCache(pfxFileInfo, password);
                }
            }
            else
            {
                _infoCache.Add(key, new CertificateInfoCache(pfxFileInfo, password));
            }
            return _infoCache[key];
        }
        private readonly Dictionary<string, CertificateInfoCache> _infoCache = [];

        /// <summary>
        /// Path where the private key may be stored
        /// for reuse when the --reuseprivatekey option
        /// is in effect
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        public FileInfo Key(Order order)
        {
            // Backwards compatible with existing keys, which are not split per order yet.
            var keyFile = new FileInfo(GetPath(order.Renewal, KeysPostfix));
            var cacheKeyVersion = 1;
            while (!keyFile.Exists && cacheKeyVersion <= MaxCacheKeyVersion)
            {
                keyFile = new FileInfo(GetPath(order.Renewal, $"-{CacheKey(order, cacheKeyVersion)}{KeysPostfix}"));
                cacheKeyVersion++;
            }
            return keyFile;
        }

        /// <summary>
        /// Save CSR to the cache
        /// </summary>
        /// <param name="order"></param>
        /// <param name="csr"></param>
        /// <returns></returns>
        public async Task StoreCsr(Order order, string csr)
        {
            ClearCache(order, CsrPostFix);
            var csrPath = new FileInfo(GetPath(order.Renewal, $"-{CacheKey(order)}{CsrPostFix}"));
            await csrPath.SafeWrite(csr);
            _log.Debug("CSR stored at {path} in certificate cache folder {folder}",
                csrPath.Name,
                csrPath.Directory?.FullName);
        }

        /// <summary>
        /// Save PFX to the cache
        /// </summary>
        /// <param name="order"></param>
        /// <param name="pfx"></param>
        /// <returns></returns>
        public async Task<ICertificateInfo> StorePfx(Order order, CertificateOption option)
        {
            ClearCache(order, postfix: $"*{PfxPostFix}");
            ClearCache($"{order.Renewal.Id}*{PfxPostFixLegacy}");

            var save = option.WithPrivateKey;
            if (_settings.Cache.ReuseDays <= 0)
            {
                save = option.WithoutPrivateKey;
            }

            var cache = await save.PfxSave(
                GetPath(order.Renewal, $"-{CacheKey(order)}{PfxPostFix}"), 
                order.Renewal.PfxPassword?.Value);
        
            if (_settings.Cache.ReuseDays <= 0)
            {
                return option.WithPrivateKey;
            }

            // Read back from cache, since we know it contains the private key
            // but now will also be an instance of CertificateInfoCache instead
            // of another implementation of ICertificateInfo. This helps it gain
            // some properties that can be used by the script installation plugin
            _log.Debug("Certificate written to cache file {path} in certificate cache folder {folder}. It will be " +
                "reused when renewing within {x} day(s) as long as the --source and --csr parameters remain the same and " +
                "the --force switch is not used.",
                cache.Name,
                cache.Directory?.FullName,
                _settings.Cache.ReuseDays);
            return FromCache(cache, order.Renewal.PfxPassword?.Value);
        }

        /// <summary>
        /// Get certificate for a specific order
        /// </summary>
        /// <param name="renewal"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        public CertificateInfoCache? PreviousInfo(Renewal renewal, string order)
        {
            var allInfos = CachedInfos(renewal).
                OrderByDescending(x => x.Certificate.NotBefore).
                ToList();
            var ret = allInfos.
                Where(c => c.CacheFile.Name.Contains($"-{order}-")).
                FirstOrDefault();
            if (ret != null)
            {
                return ret;
            }

            // Fallback to previous cache without ordername
            ret = allInfos.
                Where(c => Regex.IsMatch(c.CacheFile.Name[renewal.Id.Length..], "^-[a-f0-9]+" + Regex.Escape(PfxPostFix))).
                FirstOrDefault();
            if (ret != null)
            {
                return ret;
            }

            // Fallback to extreme legacy
            return allInfos.
                Where(c => c.CacheFile.FullName == GetPath(renewal, PfxPostFixLegacy)).
                FirstOrDefault();
        }
    }
}
