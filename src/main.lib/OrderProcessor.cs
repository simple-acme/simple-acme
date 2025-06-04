﻿using ACMESharp.Protocol.Resources;
using Autofac;
using PKISharp.WACS.Clients.Acme;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Context;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base;
using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS
{
    /// <summary>
    /// This part of the code handles the actual order process of ACME certificates
    /// </summary>
    internal class OrderProcessor(
        IAutofacBuilder scopeBuilder,
        ILogService log,
        IInputService input,
        ISettings settings,
        ICertificateService certificateService,
        ICacheService cacheService,
        RenewalValidator validator,
        DueDateRuntimeService dueDate,
        ExceptionHandler exceptionHandler,
        AcmeClient clientManager)
    {
        private readonly DueDateRuntimeService _dueDate = dueDate;

        /// <summary>
        /// Get metadata about the previous certificate and 
        /// </summary>
        /// <param name="orderContexts"></param>
        /// <returns></returns>
        internal async Task PrepareOrders(List<OrderContext> orderContexts, List<StaticOrderInfo> orderInfos)
        {
            foreach (var order in orderContexts)
            {
                // Get the previously issued certificates in this renewal
                // sub order regardless of the fact that it may have another
                // shape (e.g. different SAN names or common name etc.). This
                // means we cannot use the cache key for it.
                order.PreviousCertificate = cacheService.PreviousInfo(order.Renewal, order.OrderCacheKey);
                if (order.PreviousCertificate != null)
                {
                    log.Debug("Previous certificate found at {fi}", order.PreviousCertificate.CacheFile.FullName);
                }

                var orderInfo = orderInfos.Where(x => x.Key == order.OrderCacheKey).FirstOrDefault();
                if (orderInfo != null)
                {
                    if (orderInfo.Revoked)
                    {
                        // Never seen this exact shape of certificate yet, we should always run
                        // either because it's a new one, or because it's an order that has changed
                        // shape due to a dynamic source plugin.
                        order.ShouldRun = true;
                        log.Information(LogType.All, "Order {order} must renew because its certificate was revoked", order.OrderFriendlyName);
                        continue;
                    }
                }

                // Match using exact cache key
                order.CachedCertificate = cacheService.CachedInfo(order.Order);
                if (order.CachedCertificate != null)
                {
                    try
                    {
                        order.RenewalInfo = await clientManager.GetRenewalInfo(order.CachedCertificate);
                    } 
                    catch (Exception ex)
                    {
                        log.Error(ex, "Error getting renewal information from server");
                    }
                } 
                else 
                {
                    // Never seen this exact shape of certificate yet, we should always run
                    // either because it's a new one, or because it's an order that has changed
                    // shape due to a dynamic source plugin.
                    order.ShouldRun = true;
                    if (!order.Renewal.New)
                    {
                        log.Information(LogType.All, "Source change in order {order} detected", order.OrderFriendlyName);
                    }
                }
            }
        }

        /// <summary>
        /// Get the certificates, if not from cache then from the server
        /// </summary>
        /// <param name="context"></param>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        public async Task ExecuteOrders(List<OrderContext> context, RunLevel runLevel)
        {
            foreach (var order in context)
            {
                // Get the existing certificate matching the order description
                // this may turn out to be null even if we have found a previous
                // certificate.
                // Reason 1: the shape of the certificate changed and the cached
                // certificate no longer matches the current order.
                // Reason 2: the cache has expired and/or does not contain the
                // private key, rendering the certificate useless for installation
                // purposes.
                order.NewCertificate = GetFromCache(order, runLevel);
            }

            // Group validations of multiple order together
            // as to maximize the potential gains in parallelization
            var fromServer = context.Where(x => x.NewCertificate == null).ToList();
            foreach (var order in fromServer)
            {
                await CreateOrder(order);
            }

            // Validate all orders that need it
            var alwaysTryValidation = runLevel.HasFlag(RunLevel.Test) || runLevel.HasFlag(RunLevel.NoCache);
            var validationRequired = fromServer.Where(x => x.Order.Details != null && (x.Order.Valid == false || alwaysTryValidation));
            await validator.ValidateOrders(validationRequired, runLevel);

            // Download all the orders in parallel
            await Task.WhenAll(context.Select(async order =>
            {
                if (order.OrderResult.Success == false)
                {
                    log.Verbose("Order {n}/{m} ({friendly}): error {error}",
                         context.IndexOf(order) + 1,
                         context.Count,
                         order.OrderFriendlyName,
                         order.OrderResult.ErrorMessages?.FirstOrDefault() ?? "unknown");
                }
                else if (order.NewCertificate == null)
                {
                    log.Verbose("Order {n}/{m} ({friendly}): processing...",
                         context.IndexOf(order) + 1,
                         context.Count,
                         order.OrderFriendlyName);
                    order.NewCertificate = await GetFromServer(order);
                }
                else
                {
                    log.Verbose("Order {n}/{m} ({friendly}): handle from cache",
                         context.IndexOf(order) + 1,
                         context.Count,
                         order.OrderFriendlyName);
                }
            }));
        }

        /// <summary>
        /// Handle install/store steps
        /// </summary>
        /// <param name="orderContexts"></param>
        /// <returns></returns>
        public async Task ProcessOrders(List<OrderContext> orderContexts, RenewResult renewResult)
        {
            // Process store/install steps
            foreach (var order in orderContexts)
            {
                log.Verbose("Processing order {n}/{m}: {friendly}",
                   orderContexts.IndexOf(order) + 1,
                   orderContexts.Count,
                   order.OrderFriendlyName);

                var orderResult = order.OrderResult;
                if (order.NewCertificate == null)
                {
                    log.Error("No certificate generated");
                    orderResult.AddErrorMessage("No certificate generated");
                    continue;
                }
                orderResult.Thumbprint = order.NewCertificate.Thumbprint;
                orderResult.ExpireDate = order.NewCertificate.Certificate.NotAfter;

                // Handle installation and store
                renewResult.Abort = await ProcessOrder(order);
                if (renewResult.Abort)
                {
                    // Don't process the rest of the orders on abort
                    break;
                }

                if (orderResult.Success == false)
                {
                    // Do not try to store/install the rest of the certificates
                    // after one fails to do that
                    break;
                }

                // Store dynamically calculated due date in renewal result
                var renewalInfo = default(AcmeRenewalInfo);
                try
                {
                    renewalInfo = await clientManager.GetRenewalInfo(order.NewCertificate);
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Error getting renewal information from server");
                }
                var dueDate = _dueDate.ComputeDueDate(order.NewCertificate.Certificate, renewalInfo);
                
                // Only store in history if the server actually wants it
                // to happen earlier than what we are currently calculating
                // based on our own settings
                if (dueDate.Source?.Contains("ri") ?? false)
                {
                    orderResult.DueDate = dueDate;
                }
            }
        }

        /// <summary>
        /// Run a single order that's part of the renewal 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        private async Task<bool> ProcessOrder(OrderContext context)
        {
            try
            {
                if (context.NewCertificate == null)
                {
                    throw new InvalidOperationException();
                }

                // Early escape for testing validation only
                if (context.Renewal.New &&
                    context.RunLevel.HasFlag(RunLevel.Test) &&
                    !await input.PromptYesNo($"[--test] Store and install the certificate for order {context.OrderFriendlyName}?", true))
                {
                    return true;
                }

                // Load the store plugins
                var storeContexts = context.Renewal.StorePluginOptions.
                    Where(x => x is not Plugins.StorePlugins.NullOptions).
                    Select(x => scopeBuilder.PluginBackend<IStorePlugin, StorePluginOptions>(context.OrderScope, x)).
                    ToList();
                var storeInfo = new Dictionary<Type, StoreInfo>();
                if (!await HandleStoreAdd(context, context.NewCertificate, storeContexts, storeInfo))
                {
                    return false;
                }
                if (!await HandleInstall(context, context.NewCertificate, context.PreviousCertificate, storeInfo))
                {
                    return false;
                }
                // Success only after store and install have been done
                context.OrderResult.Success = true;

                if (context.PreviousCertificate != null &&
                    context.NewCertificate.Thumbprint != context.PreviousCertificate.Thumbprint)
                {
                    // Delete the previous certificate from the store(s)
                    await HandleStoreRemove(context, context.PreviousCertificate, storeContexts);
                }
            }
            catch (Exception ex)
            {
                var message = exceptionHandler.HandleException(ex);
                context.OrderResult.AddErrorMessage(message);
            }
            return false;
        }

        /// <summary>
        /// Get a certificate from the cache
        /// </summary>
        /// <param name="context"></param>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        private CertificateInfoCache? GetFromCache(OrderContext context, RunLevel runLevel)
        {
            var cachedCertificate = context.CachedCertificate;
            if (cachedCertificate == null)
            {
                return null;
            }
            if (cachedCertificate.CacheFile.LastWriteTime <
                DateTime.Now.AddDays(settings.Cache.ReuseDays * -1))
            {
                return null;
            }
            if (cachedCertificate.PrivateKey == null)
            {
                // Cached certificates without private keys cannot be used for 
                // new execution runs, they need to be re-ordered then
                return null;
            }
            if (runLevel.HasFlag(RunLevel.NoCache))
            {
                log.Warning(
                    "Cached certificate available but not used due to --{switch} switch.",
                    nameof(MainArguments.NoCache).ToLower());
                return null;
            }
            log.Warning(
                "Using cache for {friendlyName}. To get a new certificate " +
                "within {days} days, run with --{switch}.",
                context.Order.FriendlyNameIntermediate,
                settings.Cache.ReuseDays,
                nameof(MainArguments.NoCache).ToLower());
            return cachedCertificate;
        }

        /// <summary>
        /// Get the order from cache or place a new one at the server
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private async Task CreateOrder(OrderContext context)
        {
            log.Verbose("Obtain order details for {order}", context.OrderFriendlyName);

            // Place the order
            var orderManager = context.OrderScope.Resolve<OrderManager>();
            context.Order.KeyPath = context.Order.Renewal.CsrPluginOptions?.ReusePrivateKey == true
                ? cacheService.Key(context.Order).FullName
                : null;
            context.Order.Details = await orderManager.GetOrCreate(context.Order, clientManager, context.PreviousCertificate, context.RunLevel);

            // Sanity checks
            if (context.Order.Details == null)
            {
                context.OrderResult.AddErrorMessage($"Unable to create order");
            }
            else if (context.Order.Details.Payload.Status == AcmeClient.OrderInvalid)
            {
                context.OrderResult.AddErrorMessage($"Created order was invalid");
            }
        }

        /// <summary>
        /// Get a certificate from the server
        /// </summary>
        /// <param name="context"></param>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        private async Task<ICertificateInfo?> GetFromServer(OrderContext context)
        {
            // Generate the CSR pluginService
            var csrPlugin = context.Target.UserCsrBytes == null ? context.OrderScope.Resolve<PluginBackend<ICsrPlugin, IPluginCapability, CsrPluginOptions>>() : null;
            if (csrPlugin != null)
            {
                var state = csrPlugin.Capability.State;
                if (state.Disabled)
                {
                    context.OrderResult.AddErrorMessage($"CSR plugin is not available. {state.Disabled}");
                    return null;
                }
            }

            // Request the certificate
            try
            {
                return await certificateService.RequestCertificate(csrPlugin?.Backend, context.Order);
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error requesting certificate {friendlyName}", context.Order.FriendlyNameIntermediate);
                return null;
            }
        }

        /// <summary>
        /// Handle store plugins
        /// </summary>
        /// <param name="context"></param>
        /// <param name="newCertificate"></param>
        /// <returns></returns>
        private async Task<bool> HandleStoreAdd(
            OrderContext context,
            ICertificateInfo newCertificate,
            List<ILifetimeScope> stores,
            Dictionary<Type, StoreInfo> storeInfo)
        {
            // Run store pluginService(s)
            try
            {
                var steps = stores.Count;
                for (var i = 0; i < steps; i++)
                {
                    var store = stores[i].Resolve<PluginBackend<IStorePlugin, IPluginCapability, StorePluginOptions>>();
                    if (steps > 1)
                    {
                        log.Information("Store step {n}/{m}: {name}...", i + 1, steps, store.Meta.Name);
                    }
                    else
                    {
                        log.Information("Store with {name}...", store.Meta.Name);
                    }
                    var state = store.Capability.State;
                    if (state.Disabled)
                    {
                        context.OrderResult.AddErrorMessage($"Store plugin is not available. {state.Reason}");
                        return false;
                    }
                    var info = await store.Backend.Save(newCertificate);
                    if (info != null)
                    {
                        storeInfo.TryAdd(store.Backend.GetType(), info);
                    }
                    else
                    {
                        log.Warning("Store {name} didn't provide feedback, this may affect installation steps", store.Meta.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                var reason = exceptionHandler.HandleException(ex, "Unable to store certificate");
                context.OrderResult.AddErrorMessage($"Store failed: {reason}");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Remove previous certificate from store
        /// </summary>
        /// <param name="context"></param>
        /// <param name="previousCertificate"></param>
        /// <param name="storePluginOptions"></param>
        /// <param name="storePlugins"></param>
        /// <returns></returns>
        private async Task HandleStoreRemove(
            OrderContext context,
            ICertificateInfo previousCertificate,
            List<ILifetimeScope> stores)
        {
            for (var i = 0; i < stores.Count; i++)
            {
                var store = stores[i].Resolve<PluginBackend<IStorePlugin, IPluginCapability, StorePluginOptions>>();
                if (store.Options.KeepExisting != true)
                {
                    try
                    {
                        await store.Backend.Delete(previousCertificate);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex, "Unable to delete previous certificate");
                        // not a show-stopper, consider the renewal a success
                        context.OrderResult.AddErrorMessage($"Delete failed: {ex.Message}", false);
                    }
                }
            }
        }

        /// <summary>
        /// Handle installation steps
        /// </summary>
        /// <param name="context"></param>
        /// <param name="newCertificate"></param>
        /// <param name="previousCertificate"></param>
        /// <returns></returns>
        private async Task<bool> HandleInstall(
            OrderContext context,
            ICertificateInfo newCertificate,
            CertificateInfoCache? previousCertificate,
            Dictionary<Type, StoreInfo> storeInfo)
        {
            // Run installation pluginService(s)
            try
            {
                var installContext = context.Renewal.InstallationPluginOptions.
                    Where(x => x is not Plugins.InstallationPlugins.NullOptions).
                    Select(x => scopeBuilder.PluginBackend<IInstallationPlugin, IInstallationPluginCapability, InstallationPluginOptions>(context.OrderScope, x)).
                    ToList();

                var steps = installContext.Count;
                for (var i = 0; i < steps; i++)
                {
                    var installationPlugin = installContext[i].Resolve<PluginBackend<IInstallationPlugin, IInstallationPluginCapability, InstallationPluginOptions>>();
                    if (steps > 1)
                    {
                        log.Information("Installation step {n}/{m}: {name}...", i + 1, steps, installationPlugin.Meta.Name);
                    }
                    else
                    {
                        log.Information("Installing with {name}...", installationPlugin.Meta.Name);
                    }
                    var state = installationPlugin.Capability.State;
                    if (state.Disabled)
                    {
                        context.OrderResult.AddErrorMessage($"Installation plugin is not available. {state.Reason}");
                        return false;
                    }
                    if (!await installationPlugin.Backend.Install(storeInfo, newCertificate, previousCertificate))
                    {
                        // This is not truly fatal, other installation plugins might still be able to do
                        // something useful, and also we don't want to break compatability for users depending
                        // on scripts that return an error
                        context.OrderResult.AddErrorMessage($"Installation plugin {installationPlugin.Meta.Name} encountered an error");
                    }
                }
            }
            catch (Exception ex)
            {
                var reason = exceptionHandler.HandleException(ex, "Unable to install certificate");
                context.OrderResult.AddErrorMessage($"Install failed: {reason}");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Handle missing orders
        /// </summary>
        /// <param name="missingOrders"></param>
        /// <returns></returns>
        internal void HandleMissing(Renewal renewal, IEnumerable<string> missingOrders)
        {
            foreach (var missing in missingOrders)
            {
                cacheService.Delete(renewal, missing);
            }
        }
    }
}
