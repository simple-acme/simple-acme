﻿using ACMESharp;
using Autofac;
using Autofac.Core;
using Autofac.Features.AttributeFilters;
using PKISharp.WACS.Clients;
using PKISharp.WACS.Clients.Acme;
using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Plugins.NotificationPlugins;
using PKISharp.WACS.Plugins.Resolvers;
using PKISharp.WACS.Services.Serialization;
using PKISharp.WACS.UnitTests.Mock.Services;
using System;
using System.Collections.Generic;
using Real = PKISharp.WACS.Services;

namespace PKISharp.WACS.UnitTests.Mock
{
    internal class MockContainer
    {
        public static ILifetimeScope TestScope(List<string>? inputSequence = null, string commandLine = "")
        {
            var log = new LogService(false);
            var assemblyService = new MockAssemblyService(log);
            var pluginService = new Real.PluginService(log, assemblyService);
            var argumentsParser = new ArgumentsParser(log, assemblyService, commandLine.Split(' '));
            var input = new InputService(inputSequence ?? []);

            var builder = new ContainerBuilder();
            _ = builder.RegisterType<Real.SecretServiceManager>().SingleInstance();
            _ = builder.RegisterType<SecretService>().As<SecretService>().As<Real.ISecretService>().SingleInstance();
            _ = builder.RegisterType<AccountManager>();
            _ = builder.RegisterType<OrderManager>();
            _ = builder.RegisterType<Real.TargetValidator>();
            _ = builder.RegisterType<ZeroSsl>();
            WacsJson.Configure(builder);
            _ = builder.RegisterInstance(log).As<Real.ILogService>().As<IAcmeLogger>();
            _ = builder.RegisterInstance(argumentsParser).As<ArgumentsParser>();
            _ = builder.RegisterType<Real.ArgumentsInputService>();
            _ = builder.RegisterInstance(pluginService).As<Real.IPluginService>();
            _ = builder.RegisterInstance(input).As<Real.IInputService>();
            _ = builder.RegisterInstance(argumentsParser.GetArguments<MainArguments>()!).SingleInstance();
            _ = builder.RegisterInstance(argumentsParser.GetArguments<AccountArguments>()!).SingleInstance();
            _ = builder.RegisterType<Real.ValidationOptionsService>().As<Real.IValidationOptionsService>().SingleInstance().WithAttributeFiltering();
            _ = builder.RegisterType<Real.RenewalStore>().As<Real.IRenewalStore>().SingleInstance();
            _ = builder.RegisterType<Services.MockRenewalStore>().As<Real.IRenewalStoreBackend>().SingleInstance();
            _ = builder.RegisterType<Real.DueDateStaticService>().SingleInstance();
            _ = builder.RegisterType<Real.DueDateRuntimeService>().SingleInstance();
            _ = builder.RegisterType<Services.MockSettingsService>().As<Real.ISettingsService>().SingleInstance();
            _ = builder.RegisterType<Services.UserRoleService>().As<Real.IUserRoleService>().SingleInstance();
            _ = builder.RegisterType<Services.ProxyService>().As<Real.IProxyService>().SingleInstance();
            _ = builder.RegisterType<Real.PasswordGenerator>().SingleInstance();
            _ = builder.RegisterType<RenewalCreator>().SingleInstance();
            _ = builder.RegisterType<RenewalDescriber>().SingleInstance();
            _ = builder.RegisterType<MockRenewalRevoker>().As<Real.IRenewalRevoker>().SingleInstance();
            _ = builder.RegisterType<Real.DomainParseService>().SingleInstance();
            _ = builder.RegisterType<Mock.Clients.MockIISClient>().As<IIISClient>().SingleInstance();
            _ = builder.RegisterType<IISHelper>().SingleInstance();
            _ = builder.RegisterType<Real.ExceptionHandler>().SingleInstance();
            _ = builder.RegisterType<UnattendedResolver>();
            _ = builder.RegisterType<InteractiveResolver>();
            _ = builder.RegisterType<Real.AutofacBuilder>().As<Real.IAutofacBuilder>().SingleInstance();
            _ = builder.RegisterType<AcmeClientManager>().SingleInstance();
            _ = builder.RegisterType<ZeroSsl>().SingleInstance();
            _ = builder.RegisterType<EmailClient>().SingleInstance();
            _ = builder.RegisterType<ScriptClient>().SingleInstance();
            _ = builder.RegisterType<LookupClientProvider>().SingleInstance();
            _ = builder.RegisterType<CacheService>().As<Real.ICacheService>().SingleInstance();
            _ = builder.RegisterType<CertificateService>().As<Real.ICertificateService>().SingleInstance();
            if (OperatingSystem.IsWindows())
            {
                _ = builder.RegisterType<Real.TaskSchedulerService>().As<Real.IAutoRenewService>().SingleInstance();
            }
            _ = builder.RegisterType<Real.NotificationService>().SingleInstance();
            _ = builder.RegisterType<NotificationTargetEmail>().SingleInstance();
            _ = builder.RegisterType<RenewalValidator>().SingleInstance();
            _ = builder.RegisterType<RenewalExecutor>().SingleInstance();
            _ = builder.RegisterType<OrderProcessor>().SingleInstance();
            _ = builder.RegisterType<RenewalManager>().SingleInstance();
            _ = builder.Register(c => (ISharingLifetimeScope)c.Resolve<ILifetimeScope>()).As<ISharingLifetimeScope>().ExternallyOwned();

            var ret = builder.Build();
            return ret.BeginLifetimeScope("wacs", builder =>
            {
                // Plugins
                foreach (var plugin in pluginService.GetSecretServices())
                {
                    _ = builder.RegisterType(plugin.Backend);
                }
            });
        }
    }
}
