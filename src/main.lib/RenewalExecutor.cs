﻿using Autofac;
using Autofac.Core;
using PKISharp.WACS.Clients;
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
    /// This part of the code handles the actual creation/renewal 
    /// </summary>
    internal class RenewalExecutor(
        MainArguments args,
        IAutofacBuilder scopeBuilder,
        ILogService log,
        IInputService input,
        ISettings settings,
        TargetValidator targetValidator,
        DueDateStaticService dueDateStatic,
        DueDateRuntimeService dueDateRuntime,
        ExceptionHandler exceptionHandler,
        IAutoRenewService taskScheduler,
        AcmeClientManager clientManager,
        ISharingLifetimeScope container)
    {
        private readonly ILifetimeScope _container = container;

        /// <summary>
        /// Determine if the renewal should be executed
        /// </summary>
        /// <param name="renewal"></param>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        public async Task<RenewResult> HandleRenewal(Renewal renewal, RunLevel runLevel)
        {
            input.CreateSpace();
            log.Reset();

            // Check the initial, combined target for the renewal
            var client = await clientManager.GetClient(renewal.Account);
            using var es = scopeBuilder.Execution(_container, renewal, client, runLevel);
            var targetPlugin = es.Resolve<PluginBackend<ITargetPlugin, IPluginCapability, TargetPluginOptions>>();
            if (targetPlugin.Capability.State.Disabled)
            {
                return new RenewResult($"Source plugin {targetPlugin.Meta.Name} is disabled. {targetPlugin.Capability.State.Reason}");
            }
            var target = await targetPlugin.Backend.Generate();
            if (target == null)
            {
                log.Information("Plugin {targetPluginName} did not generate a source", targetPlugin.Meta.Name);
                return new RenewResult($"Plugin {targetPlugin.Meta.Name} did not generate a source");
            }
            log.Information("Plugin {targetPluginName} generated source {common} with {n} identifiers",
                targetPlugin.Meta.Name, 
                target.DisplayName.Value,
                target.Parts.SelectMany(p => p.Identifiers).Distinct().Count());

            // Create one or more orders from the target
            var targetScope = scopeBuilder.Split(es, target);
            var orderPlugin = targetScope.Resolve<PluginBackend<IOrderPlugin, IPluginCapability, OrderPluginOptions>>();
            var orders = orderPlugin.Backend.Split(renewal, target);
            if (orders == null || orders.Count == 0)
            {
                return new RenewResult($"Order plugin {orderPlugin.Meta.Name} failed to create order(s)");
            }
            log.Information($"Plugin {{order}} created {{n}} order{(orders.Count > 1?"s":"")}", orderPlugin.Meta.Name, orders.Count);
            foreach (var order in orders)
            {
                if (!targetValidator.IsValid(order.Target))
                {
                    var blame = orders.Count > 1 ? "Order" : "Source";
                    var blamePlugin = orders.Count > 1 ? orderPlugin.Meta : targetPlugin.Meta;
                    return new RenewResult($"{blame} plugin {blamePlugin.Name} created invalid source");
                }
            }

            // Logging
            if (!runLevel.HasFlag(RunLevel.Force) && !renewal.Updated)
            {
                log.Verbose("Checking {renewal}", renewal.LastFriendlyName);
            }
            else if (runLevel.HasFlag(RunLevel.Force))
            {
                log.Information(LogType.All, "Force renewing {renewal}", renewal.LastFriendlyName);
            }

            // Handle the orders
            var result = await HandleOrders(es, renewal, orders, runLevel);

            // Manage the task scheduler
            await ManageTaskScheduler(renewal, result, runLevel);

            return result;
        }

        /// <summary>
        /// Optionally ensure the task scheduler, depending on renewal result and 
        /// various other switches and settings.
        /// </summary>
        /// <param name="renewal"></param>
        /// <param name="result"></param>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        private async Task ManageTaskScheduler(Renewal renewal, RenewResult result, RunLevel runLevel)
        {
            // Configure task scheduler
            if (args.SetupTaskScheduler)
            {
                runLevel |= RunLevel.ForceTaskScheduler;
            }
            if (args.NoTaskScheduler)
            {
                runLevel |= RunLevel.NoTaskScheduler;
            }
            if (runLevel.HasFlag(RunLevel.NoTaskScheduler))
            {
                return;
            }
            if (result.Success == false)
            {
                return;
            }

            // Unless specifically flagged not to setup task scheduler,
            // --test mode will ask the question whether to setup the 
            // task or not.
            if (!result.Abort &&
                (renewal.New || renewal.Updated) &&
                runLevel.HasFlag(RunLevel.Test) &&
                !await input.PromptYesNo($"[--test] Do you want to automatically renew with these settings?", true))
            {
                result.Abort = true;
                return;
            }

            // Attempt to create the scheduled task
            try
            {
                await taskScheduler.EnsureAutoRenew(runLevel);
            }
            catch (Exception ex)
            {
                exceptionHandler.HandleException(ex, "Unable to configure automatic renewals");
            }
        }

        /// <summary>
        /// Return abort result
        /// </summary>
        /// <param name="renewal"></param>
        /// <returns></returns>
        private RenewResult Abort(Renewal renewal, RenewResult result)
        {
            var dueDate = dueDateStatic.DueDate(renewal);
            if (dueDate != null)
            {
                // For sure now that we don't need to run so abort this execution
                log.Information("Renewal {renewal} is due after {date}", renewal.LastFriendlyName, input.FormatDate(dueDate.Start));
            }
            result.Abort = true;
            return result;
        }

        /// <summary>
        /// Run the renewal 
        /// </summary>
        /// <param name="execute"></param>
        /// <param name="orders"></param>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        private async Task<RenewResult> HandleOrders(ILifetimeScope execute, Renewal renewal, List<Order> orders, RunLevel runLevel)
        {
            // Return value
            var result = new RenewResult() { OrderResults = [] };

            // Get the certificates from cache or server
            var orderProcessor = execute.Resolve<OrderProcessor>();

            // Build context
            var previousOrders = dueDateStatic.CurrentOrders(renewal);
            var orderContexts = orders.Select(order => new OrderContext(scopeBuilder.Order(execute, order), order, runLevel)).ToList();
            await orderProcessor.PrepareOrders(orderContexts, previousOrders);

            // Check individual orders
            foreach (var o in orderContexts)
            {
                if (o.ShouldRun)
                {
                    log.Verbose("Order {name} should run (new/changed source)", o.OrderFriendlyName);
                }
                else if (runLevel.HasFlag(RunLevel.Force))
                {
                    o.ShouldRun = true;
                    log.Verbose("Order {name} should run (forced)", o.OrderFriendlyName);
                }
                else if (dueDateRuntime.ShouldRun(o))
                {
                    o.ShouldRun = true;
                    log.Verbose("Order {name} should run (due for renewal)", o.OrderFriendlyName);
                }
                else
                {
                    log.Verbose("Order {name} should not run this time", o.OrderFriendlyName);
                }
            }

            // Check missing orders
            var missingOrders = previousOrders.Where(x => !orderContexts.Any(c => c.OrderCacheKey == x.Key));
            if (missingOrders.Any())
            {
                foreach (var order in missingOrders)
                {
                    // This order was previously included in the set
                    // but has now disappeared, i.e. because bindings
                    // in IIS have changed or a new CSR was placed.
                    // We will note this in the renewal history, so that
                    // we won't take them into account anymore in the
                    // DueDateStaticService.
                    result.OrderResults.Add(new OrderResult(order.Key) { Missing = true });
                }
            }

            // Only process orders that are due. 
            var runnableContexts = orderContexts;
            if (!runLevel.HasFlag(RunLevel.NoCache) && !renewal.New && !renewal.Updated)
            {
                runnableContexts = orderContexts.Where(x => x.ShouldRun).ToList();
            }
            if (runnableContexts.Count == 0)
            {
                log.Debug("None of the orders are currently due to run");
                return Abort(renewal, result);
            }

            // Store results
            result.OrderResults.AddRange(runnableContexts.Select(x => x.OrderResult));

            if (!renewal.New && !runLevel.HasFlag(RunLevel.Force))
            {
                log.Information(LogType.All, "Renewing {renewal}", renewal.LastFriendlyName);
            }
            if (orders.Count > runnableContexts.Count)
            {
                log.Information("{n} of {m} orders are due to run", runnableContexts.Count, orders.Count);
            }

            // If at this point we haven't retured already with an error/abort
            // actually execute the renewal

            // Run the pre-execution script, e.g. to re-configure
            // local firewall rules, since now it's (almost) sure
            // that we're going to do something. Actually we may
            // still be able to read all certificates from cache,
            // but that's the exception rather than the rule.
            var preScript = settings.Execution.DefaultPreExecutionScript;
            var scriptClient = execute.Resolve<ScriptClient>();
            if (!string.IsNullOrWhiteSpace(preScript))
            {
                await scriptClient.RunScript(preScript, $"{renewal.Id}");
            }
            await orderProcessor.ExecuteOrders(runnableContexts, runLevel);


            // Handle all the store/install steps
            await orderProcessor.ProcessOrders(runnableContexts, result);

            // Run the post-execution script. Note that this is different
            // from the script installation pluginService, which is handled
            // in the previous step. This is only meant to undo any
            // (firewall?) changes made by the pre-execution script.
            var postScript = settings.Execution.DefaultPostExecutionScript;
            if (!string.IsNullOrWhiteSpace(postScript))
            {
                await scriptClient.RunScript(postScript, $"{renewal.Id}");
            }

            // Handle missing order (clear cache)
            orderProcessor.HandleMissing(renewal, missingOrders.Select(m => m.Key));

            // Return final result
            result.Success = runnableContexts.All(o => o.OrderResult.Success == true);
            return result;
        }
    }
}
