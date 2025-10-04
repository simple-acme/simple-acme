using Autofac;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base;
using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Plugins.CsrPlugins;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.TargetPlugins;
using PKISharp.WACS.Plugins.ValidationPlugins.Http;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.Resolvers
{
    internal class UnattendedResolver(
        ILogService log,
        ISettings settings,
        IAutofacBuilder autofacBuilder,
        ILifetimeScope scope,
        MainArguments arguments,
        IPluginService pluginService) : IResolver
    {
        [DebuggerDisplay("{Meta.Name}")]
        private record PluginChoice<TCapability, TOptions>(
            Plugin Meta,
            PluginFrontend<TCapability, TOptions> Frontend,
            State ConfigurationState,
            State ExecutionState)
            where TOptions : PluginOptions, new()
            where TCapability : IPluginCapability;

        private Task<PluginFrontend<TCapability, TOptions>?> 
            GetPlugin<TCapability, TOptions>(
                Steps step,
                Type defaultBackend,
                string? defaultParam1 = null,
                string? defaultParam2 = null,
                Func<TCapability, State>? configState = null)
                where TOptions : PluginOptions, new()
                where TCapability : IPluginCapability
        {
            // Helper method to determine final usability state
            // combination of plugin being enabled (e.g. due to missing
            // administrator rights) and being a right fit for the current
            // renewal (e.g. cannot validate wildcards using http-01)
            State combinedConfigState(PluginFrontend<TCapability, TOptions> plugin)
            {
                var baseState = plugin.Capability.ConfigurationState;
                if (baseState.Disabled)
                {
                    return baseState;
                }
                else if (configState != null)
                {
                    return configState(plugin.Capability);
                }
                return State.EnabledState();
            };

            // Apply default sorting when no sorting has been provided yet
            var options = pluginService.
                GetPlugins(step).
                Select(x => autofacBuilder.PluginFrontend<TCapability, TOptions>(scope, x)).
                Select(x => x.Resolve<PluginFrontend<TCapability, TOptions>>()).
                Select(x => new PluginChoice<TCapability, TOptions>(x.Meta, x, combinedConfigState(x), x.Capability.ExecutionState)).
                ToList();

            // Default out when there are no reasonable plugins to pick
            var nullRet = Task.FromResult<PluginFrontend<TCapability, TOptions>?>(null);
            if (options.Count == 0 || options.All(x => x.ConfigurationState.Disabled))
            {
                return nullRet;
            }

            var className = step.ToString().ToLower();
            if (!string.IsNullOrEmpty(defaultParam1))
            {
                var defaultPlugin = pluginService.GetPlugin(step, defaultParam1, defaultParam2);
                if (defaultPlugin == null)
                {
                    log.Error("Unable to find {n} plugin {p}. Choose another plugin using the {className} switch or change the default in settings.json", step, defaultParam1, $"--{className}");
                    return nullRet;
                }
                else
                {
                    defaultBackend = defaultPlugin.Backend;
                }
            }

            var defaultOption = options.OrderBy(x => x.Meta.Hidden).First(x => x.Meta.Backend == defaultBackend);
            if (defaultOption.ConfigurationState.Disabled)
            {
                log.Error("{n} plugin {x} not available: {m}. Choose another plugin using the {className} switch or change the default in settings.json", step, defaultOption.Frontend.Meta.Name ?? "Unknown", defaultOption.ConfigurationState.Reason?.TrimEnd('.'), $"--{className}");
                return nullRet;
            }
            if (defaultOption.ExecutionState.Disabled)
            {
                log.Warning("{n} plugin {x} might not work: {m}. If this leads to an error, choose another plugin using the {className} switch or change the default in settings.json", step, defaultOption.Frontend.Meta.Name ?? "Unknown", defaultOption.ExecutionState.Reason?.TrimEnd('.'), $"--{className}");
            }
            return Task.FromResult<PluginFrontend<TCapability, TOptions>?>(defaultOption.Frontend);
        }

        /// <summary>
        /// Get the TargetPlugin which was used (or can be assumed to have been used) to create this
        /// Renewal
        /// </summary>
        /// <returns></returns>
        public async Task<PluginFrontend<IPluginCapability, TargetPluginOptions>?> GetTargetPlugin()
        {
            // NOTE: checking the default option here doesn't make 
            // sense because MainArguments.Source is what triggers
            // unattended mode in the first place. We woudn't even 
            // get into this code unless it was specified.
            return await GetPlugin<IPluginCapability, TargetPluginOptions>(
                Steps.Source,
                defaultParam1: string.IsNullOrWhiteSpace(arguments.Source) ? arguments.Target : arguments.Source,
                defaultBackend: typeof(Manual));
        }

        /// <summary>
        /// Get the ValidationPlugin which was used (or can be assumed to have been used) 
        /// to validate this Renewal
        /// </summary>
        /// <returns></returns>
        public async Task<PluginFrontend<IValidationPluginCapability, ValidationPluginOptions>?> GetValidationPlugin()
        {
            return await GetPlugin<IValidationPluginCapability, ValidationPluginOptions>(
                Steps.Validation,
                defaultParam1: arguments.Validation ?? settings.Validation.DefaultValidation,
                defaultParam2: arguments.ValidationMode ?? settings.Validation.DefaultValidationMode,
                defaultBackend: typeof(SelfHosting));
        }

        /// <summary>
        /// Get the OrderPlugin which is used to convert the target into orders 
        /// and request the certificate
        /// </summary>
        /// <returns></returns>
        public async Task<PluginFrontend<IPluginCapability, OrderPluginOptions>?> GetOrderPlugin()
        {
            return await GetPlugin<IPluginCapability, OrderPluginOptions>(
                Steps.Order,
                defaultParam1: arguments.Order,
                defaultBackend: typeof(OrderPlugins.Single));
        }

        /// <summary>
        /// Get the CsrPlugin which is used to generate the private key 
        /// and request the certificate
        /// </summary>
        /// <returns></returns>
        public async Task<PluginFrontend<IPluginCapability, CsrPluginOptions>?> GetCsrPlugin()
        {
            return await GetPlugin<IPluginCapability, CsrPluginOptions>(
                Steps.Csr,
                defaultParam1: arguments.Csr,
                defaultBackend: typeof(Rsa));
        }

        /// <summary>
        /// Get the StorePlugin which is used to persist the certificate
        /// </summary>
        /// <returns></returns>
        public async Task<PluginFrontend<IPluginCapability, StorePluginOptions>?> GetStorePlugin(IEnumerable<Plugin> chosen)
        {
            var defaultStore = arguments.Store ?? settings.Store.DefaultStore;
            var parts = defaultStore.ParseCsv();
            if (parts == null)
            {
                return null;
            }
            var index = chosen.Count();
            defaultStore = index == parts.Count ? StorePlugins.Null.Name : parts[index];
            return await GetPlugin<IPluginCapability, StorePluginOptions>(
                Steps.Store,
                defaultParam1: defaultStore,
                defaultBackend: typeof(StorePlugins.Null));
        }

        /// <summary>
        /// Get the InstallationPlugin which was used (or can be assumed to have been used) to install 
        /// this ScheduledRenewal
        /// </summary>
        /// <returns></returns>
        public async Task<PluginFrontend<IInstallationPluginCapability, InstallationPluginOptions>?> 
            GetInstallationPlugin(Plugin source, IEnumerable<Plugin> stores, IEnumerable<Plugin> installation)
        {
            var installationList = arguments.Installation ?? settings.Installation.DefaultInstallation;
            if (string.IsNullOrWhiteSpace(installationList) && OperatingSystem.IsWindows() && IIS.IDs.Contains(source.Id.ToString()))
            { 
                // If source is IIS, and install is not specified,
                // assume that user intends to install with IIS as 
                // well. Users who don't want this (are there any?)
                // can work around this with --installation none
                installationList = InstallationPlugins.IIS.Trigger;
            }
            var steps = installationList.ParseCsv();
            steps ??= [InstallationPlugins.Null.Trigger];
            var index = installation.Count();
            var currentStep = index == steps.Count ? InstallationPlugins.Null.Trigger : steps[index];
            return await GetPlugin<IInstallationPluginCapability, InstallationPluginOptions>(
                Steps.Installation,
                configState: x => x.CanInstall(stores.Select(x => x.Backend), installation.Select(x => x.Backend)),
                defaultParam1: currentStep,
                defaultBackend: typeof(InstallationPlugins.Null));
        }
    }
}
