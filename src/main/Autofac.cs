using ACMESharp;
using Autofac;
using Autofac.Core;
using PKISharp.WACS.Clients;
using PKISharp.WACS.Clients.Acme;
using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Plugins.Resolvers;
using PKISharp.WACS.Plugins.ValidationPlugins.Http;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.AutoRenew;
using PKISharp.WACS.Services.Serialization;
using System;

namespace PKISharp.WACS.Host
{
    internal static class Autofac
    {
        /// <summary>
        /// Configure dependency injection container
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        internal static ILifetimeScope Container(string[] args, bool verbose, bool config)
        {
            var builder = new ContainerBuilder();
            _ = builder.RegisterType<LogService>().WithParameter(new NamedParameter(nameof(verbose), verbose)).WithParameter(new NamedParameter(nameof(config), config)).SingleInstance().As<ILogService>().As<IAcmeLogger>();
            _ = builder.RegisterType<ExtendedAssemblyService>().As<AssemblyService>().SingleInstance();
            _ = builder.RegisterType<PluginService>().SingleInstance().As<IPluginService>();
            _ = builder.RegisterType<ArgumentsParser>().WithParameter(new TypedParameter(typeof(string[]), args)).SingleInstance();
            _ = builder.RegisterType<SettingsService>().SingleInstance();
            var plugin = builder.Build();

            var pluginService = plugin.Resolve<IPluginService>();
            return plugin.BeginLifetimeScope("wacs", builder =>
            {
                // Plugins
                foreach (var plugin in pluginService.GetPlugins()) {
                    _ = builder.RegisterType(plugin.OptionsJson);
                }                
                foreach (var plugin in pluginService.GetSecretServices()) {
                    _ = builder.RegisterType(plugin.Backend);
                }
                foreach (var plugin in pluginService.GetNotificationTargets()) {
                    _ = builder.RegisterType(plugin.Backend);
                }
                WacsJson.Configure(builder);

                // Single instance types
                _ = builder.RegisterType<AdminService>().SingleInstance();
                _ = builder.RegisterType<VersionService>().SingleInstance();
                _ = builder.RegisterType<HelpService>().SingleInstance();
                _ = builder.RegisterType<UserRoleService>().As<IUserRoleService>().SingleInstance();
                _ = builder.RegisterType<ValidationOptionsService>().As<IValidationOptionsService>().As<ValidationOptionsService>().SingleInstance();
                _ = builder.RegisterType<InputService>().As<IInputService>().SingleInstance();
                _ = builder.RegisterType<ProxyService>().As<IProxyService>().SingleInstance();
                _ = builder.RegisterType<UpdateClient>().SingleInstance();
                _ = builder.RegisterType<RenewalStoreDisk>().As<IRenewalStoreBackend>().SingleInstance();
                _ = builder.RegisterType<RenewalStore>().As<IRenewalStore>().SingleInstance();
                _ = builder.RegisterType<DomainParseService>().SingleInstance();
                _ = builder.RegisterType<IISClient>().As<IIISClient>().InstancePerLifetimeScope();
                _ = builder.RegisterType<IISHelper>().SingleInstance();
                _ = builder.RegisterType<ExceptionHandler>().SingleInstance();
                _ = builder.RegisterType<AutofacBuilder>().As<IAutofacBuilder>().SingleInstance();
                _ = builder.RegisterType<AccountManager>().SingleInstance();
                _ = builder.RegisterType<AcmeClientManager>().SingleInstance();
                _ = builder.RegisterType<NetworkCheckService>().SingleInstance();
                _ = builder.RegisterType<ZeroSsl>().SingleInstance();
                _ = builder.RegisterType<OrderManager>().SingleInstance();
                _ = builder.RegisterType<TargetValidator>().SingleInstance();
                _ = builder.RegisterType<EmailClient>().SingleInstance();
                _ = builder.RegisterType<ScriptClient>().SingleInstance();
                _ = builder.RegisterType<LookupClientProvider>().SingleInstance();
                _ = builder.RegisterType<CacheService>().As<ICacheService>().SingleInstance();
                _ = builder.RegisterType<CertificatePicker>().SingleInstance();
                _ = builder.RegisterType<DueDateStaticService>().SingleInstance();
                _ = builder.RegisterType<DueDateRuntimeService>().SingleInstance();
                _ = builder.RegisterType<SecretServiceManager>().SingleInstance();
                if (OperatingSystem.IsWindows())
                {
                    _ = builder.RegisterType<TaskSchedulerService>().As<IAutoRenewService>().SingleInstance();
                }
                else if (OperatingSystem.IsLinux())
                {
                    _ = builder.RegisterType<CronJobService>().As<IAutoRenewService>().SingleInstance();
                }
                _ = builder.RegisterType<NotificationService>().SingleInstance();
                _ = builder.RegisterType<RenewalExecutor>().SingleInstance();
                _ = builder.RegisterType<RenewalManager>().SingleInstance();
                _ = builder.RegisterType<RenewalCreator>().SingleInstance();
                _ = builder.RegisterType<RenewalDescriber>().SingleInstance();
                _ = builder.RegisterType<RenewalRevoker>().As<IRenewalRevoker>().SingleInstance();
                _ = builder.RegisterType<Unattended>().SingleInstance();
                _ = builder.RegisterType<ArgumentsInputService>().SingleInstance();
                _ = builder.RegisterType<MainMenu>().SingleInstance();
                _ = builder.RegisterType<Banner>().SingleInstance();

                // Multi-instance types
                _ = builder.RegisterType<Wacs>();
                _ = builder.RegisterType<UnattendedResolver>();
                _ = builder.RegisterType<InteractiveResolver>();

                // Specials
                _ = builder.RegisterType<HttpValidationParameters>().InstancePerLifetimeScope();
                _ = builder.Register(c => c.Resolve<ArgumentsParser>().GetArguments<MainArguments>()!);
#pragma warning disable CS0618 // Type or member is obsolete
                _ = builder.Register(c => c.Resolve<SettingsService>().Settings).As<ISettings>().As<ISettingsService>();
#pragma warning restore CS0618 // Type or member is obsolete
                _ = builder.Register(c => c.Resolve<ArgumentsParser>().GetArguments<AccountArguments>()!);
                _ = builder.Register(c => (ISharingLifetimeScope)c.Resolve<ILifetimeScope>()).As<ISharingLifetimeScope>().ExternallyOwned();
            });
        }
    }
}