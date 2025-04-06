﻿using ACMESharp.Protocol;
using ACMESharp.Protocol.Resources;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PKISharp.WACS.Clients.Acme
{
    /// <summary>
    /// The OrderManager makes sure that we don't hit rate limits
    /// </summary>
    internal class OrderManager(ILogService log, ISettingsService settings)
    {
        private readonly DirectoryInfo _orderPath = settings.Valid ?
                new DirectoryInfo(Path.Combine(settings.Client.ConfigurationPath, "Orders")) :
                new DirectoryInfo(Directory.GetCurrentDirectory());
        private const string _orderFileExtension = "order.json";
        private const string _orderKeyExtension = "order.keys";

        /// <summary>
        /// To check if it's possible to reuse a previously retrieved
        /// certificate we create a hash of its key properties and included
        /// that hash in the file name. If we get the same hash on a 
        /// subsequent run, it means it's safe to reuse (no relevant changes).
        /// </summary>
        /// <param name="renewal"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        private static string CacheKey(Order order, string accountId)
        {
            // Check if we can reuse a cached order based on currently
            // active set of parameters and shape of 
            // the target.
            var cacheKeyBuilder = new StringBuilder();
            cacheKeyBuilder.Append(accountId);
            cacheKeyBuilder.Append(order.Target.CommonName);
            cacheKeyBuilder.Append(string.Join(',', order.Target.GetIdentifiers(true).OrderBy(x => x).Select(x => x.Value.ToLower())));
            _ = order.Target.UserCsrBytes != null ?
                cacheKeyBuilder.Append(Convert.ToBase64String(order.Target.UserCsrBytes.ToArray())) :
                cacheKeyBuilder.Append('-');
            _ = order.Renewal.CsrPluginOptions != null ?
                cacheKeyBuilder.Append(JsonSerializer.Serialize(order.Renewal.CsrPluginOptions, WacsJson.Insensitive.CsrPluginOptions)) :
                cacheKeyBuilder.Append('-');
            cacheKeyBuilder.Append(order.KeyPath);
            return cacheKeyBuilder.ToString().SHA1();
        }

        /// <summary>
        /// Get a previously cached order or if its too old, create a new one
        /// </summary>
        /// <param name="renewal"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public async Task<AcmeOrderDetails?> GetOrCreate(Order order, AcmeClient client, ICertificateInfo? replaces, RunLevel runLevel)
        {
            var cacheKey = CacheKey(order, client.Account.Details.Kid);
            if (settings.Cache.ReuseDays > 0)
            {
                // Above conditional not only prevents us from reading a cached
                // order from disk, but also prevent the "KeyPath" property from
                // being set in the first place, which in turn prevents the
                // CsrPlugin from caching the private key on disk.
                if (string.IsNullOrWhiteSpace(order.KeyPath))
                {
                    order.KeyPath = Path.Combine(_orderPath.FullName, $"{cacheKey}.{_orderKeyExtension}");
                }
                var orderDetails = await GetFromCache(cacheKey, client, runLevel);
                if (orderDetails != null)
                {
                    var keyFile = new FileInfo(order.KeyPath);
                    if (keyFile.Exists)
                    {
                        log.Warning("Using cache. To force a new order within {days} days, " +
                              "run with --{switch}. Beware that you might run into rate limits.",
                              settings.Cache.ReuseDays,
                              nameof(MainArguments.NoCache).ToLower());
                        return orderDetails;
                    }
                    else
                    {
                        log.Debug("Cached order available but not used.");
                    }
                }
            }
            return await CreateOrder(cacheKey, client, replaces, order.Target);
        }

        /// <summary>
        /// Delete all relevant files from the order cache
        /// </summary>
        /// <param name="cacheKey"></param>
        private void DeleteFromCache(string cacheKey)
        {
            DeleteFile($"{cacheKey}.{_orderFileExtension}");
            DeleteFile($"{cacheKey}.{_orderKeyExtension}");
        }

        /// <summary>
        /// Delete a file from the order cache
        /// </summary>
        /// <param name="path"></param>
        private void DeleteFile(string path)
        {
            var fileInfo = new FileInfo(Path.Combine(_orderPath.FullName, path));
            if (fileInfo.Exists)
            {
                fileInfo.Delete();
                log.Debug("Deleted {fileInfo}", fileInfo.FullName);
            }
        }

        /// <summary>
        /// Get order from the cache
        /// </summary>
        /// <param name="cacheKey"></param>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        private async Task<AcmeOrderDetails?> GetFromCache(string cacheKey, AcmeClient client, RunLevel runLevel)
        {
            var existingOrder = FindRecentOrder(cacheKey);
            if (existingOrder == null)
            {
                log.Verbose("No existing order found");
                return null;
            }

            if (runLevel.HasFlag(RunLevel.NoCache))
            {
                // Delete previously cached order
                // and previously cached key as well
                // to ensure that it won't be used
                log.Warning("Cached order available but not used with --{switch} option.",
                    nameof(MainArguments.NoCache).ToLower());
                if (existingOrder.Payload.Authorizations != null)
                {
                    foreach (var auth in existingOrder.Payload.Authorizations)
                    {
                        try
                        {
                            log.Debug("Deactivating pre-existing authorization");
                            await client.DeactivateAuthorization(auth);
                        }
                        catch (Exception ex)
                        {
                            log.Warning(ex, "Error deactivating pre-existing authorization");
                        }
                    }
                }

                DeleteFromCache(cacheKey);
                return null;
            }

            try
            {
                log.Debug("Refreshing cached order");
                existingOrder = await RefreshOrder(existingOrder, client);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Unable to refresh cached order");
                DeleteFromCache(cacheKey);
                return null;
            }

            if (existingOrder.Payload.Status != AcmeClient.OrderValid &&
                existingOrder.Payload.Status != AcmeClient.OrderReady)
            {
                log.Warning("Cached order has status {status}, discarding", existingOrder.Payload.Status);
                DeleteFromCache(cacheKey);
                return null;
            }
            
            // Make sure that the CsrBytes and PrivateKey are available
            // for this order
            return existingOrder;
        }

        /// <summary>
        /// Update order details from the server
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        private async Task<AcmeOrderDetails> RefreshOrder(AcmeOrderDetails order, AcmeClient client)
        {
            log.Debug("Refreshing order...");
            if (order.OrderUrl == null) 
            {
                throw new InvalidOperationException("Missing order url");
            }
            var update = await client.GetOrderDetails(order.OrderUrl);
            order.Payload = update.Payload;
            return order;
        }

        /// <summary>
        /// Create new order at the server
        /// </summary>
        /// <param name="cacheKey"></param>
        /// <param name="csrPlugin"></param>
        /// <param name="privateKeyFile"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        private async Task<AcmeOrderDetails?> CreateOrder(string cacheKey, AcmeClient client, ICertificateInfo? previous, Target target)
        {
            try
            {
                // Determine final shape of the certificate
                var identifiers = target.GetIdentifiers(false);

                // Determine notAfter value (unsupported by Let's
                // Encrypt at this time, but should work at Sectigo
                // and possibly others
                var validDays = settings.Order.DefaultValidDays;
                // Certificates use UTC 
                var now = DateTime.UtcNow; 
                // We don't want milliseconds/ticks
                var nowRound = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Kind);
                var notAfter = validDays != null ?
                    nowRound.AddDays(validDays.Value) : 
                    (DateTime?)null;

                // Create the order
                var order = default(AcmeOrderDetails?);
                try
                {
                    order = await client.CreateOrder(identifiers, previous, notAfter);
                }
                catch (AcmeProtocolException ex)
                {
                    if (previous != null && ex.ProblemType == ProblemType.Conflict)
                    {
                        log.Warning("This order has already been replaced, possibly due to multiple renewals generating the same certificate. You may use the Renewal Manager to scan for duplicates.");
                        order = await client.CreateOrder(identifiers, null, notAfter);
                    }
                    else
                    {
                        throw;
                    }
                }

                if (order.Payload.Error != default)
                {
                    log.Error("Failed to create order {url}: {detail}", order.OrderUrl, order.Payload.Error.Detail);
                    return null;
                }
                
                log.Verbose("Order {url} created", order.OrderUrl);
                await SaveOrder(order, cacheKey);
                return order;
            } 
            catch (AcmeProtocolException ex)
            {
                log.Error($"Failed to create order: {ex.ProblemDetail ?? ex.Message}");
            }
            catch (Exception ex)
            {
                log.Error(ex, $"Failed to create order");
            }
            return null;
        }

        /// <summary>
        /// Check if we have a recent order that can be reused
        /// to prevent hitting rate limits
        /// </summary>
        /// <param name="identifiers"></param>
        /// <returns></returns>
        private AcmeOrderDetails? FindRecentOrder(string cacheKey)
        {
            DeleteStaleFiles();
            var fi = new FileInfo(Path.Combine(_orderPath.FullName, $"{cacheKey}.{_orderFileExtension}"));
            if (!fi.Exists || !IsValid(fi))
            {
                return null;
            }
            try
            {
                var content = File.ReadAllText(fi.FullName);
                var order = JsonSerializer.Deserialize(content, AcmeClientJson.Insensitive.AcmeOrderDetails);
                return order;
            } 
            catch (Exception ex)
            {
                log.Warning(ex, "Unable to read order cache");
            }
            return null;
        }

        /// <summary>
        /// Delete files that are not valid anymore
        /// </summary>
        private void DeleteStaleFiles()
        {
            if (_orderPath.Exists)
            {
                var orders = new[] { 
                    $"*.{_orderFileExtension}",
                    $"*.{_orderKeyExtension}"
                }.SelectMany(_orderPath.EnumerateFiles);
                foreach (var order in orders)
                {
                    if (!IsValid(order))
                    {
                        try
                        {
                            order.Delete();
                        }
                        catch (Exception ex)
                        {
                            log.Debug("Unable to clean up order cache: {ex}", ex.Message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Test if a cached order file is still usable
        /// </summary>
        /// <returns></returns>
        private bool IsValid(FileInfo order) => order.LastWriteTime > DateTime.Now.AddDays(settings.Cache.ReuseDays * -1);

        /// <summary>
        /// Save order to disk
        /// </summary>
        /// <param name="order"></param>
        private async Task SaveOrder(AcmeOrderDetails order, string cacheKey)
        {
            try
            {
                if (settings.Cache.ReuseDays <= 0)
                {
                    return;
                }
                if (!_orderPath.Exists)
                {
                    _orderPath.Create();
                }
                var content = JsonSerializer.Serialize(order, AcmeClientJson.Default.AcmeOrderDetails);
                var path = Path.Combine(_orderPath.FullName, $"{cacheKey}.{_orderFileExtension}");
                await FileInfoExtensions.SafeWrite(path, content);
            }
            catch (Exception ex)
            {
                log.Warning(ex, "Unable to write to order cache");
            }
        }

        /// <summary>
        /// Encrypt or decrypt the cached private keys
        /// </summary>
        public async Task Encrypt()
        {
            foreach (var f in _orderPath.EnumerateFiles($"*.{_orderKeyExtension}"))
            {
                var x = new ProtectedString(File.ReadAllText(f.FullName), log);
                log.Information("Rewriting {x}", f.Name);
                await f.SafeWrite(x.DiskValue(settings.Security.EncryptConfig));
            }
        }

        /// <summary>
        /// Delete all orders from cache
        /// </summary>
        internal void ClearCache()
        {
            foreach (var f in _orderPath.EnumerateFiles($"*.*"))
            {
                f.Delete();
            }
        }
    }
}
