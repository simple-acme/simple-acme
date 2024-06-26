using ACMESharp.Authorizations;
using ACMESharp.Protocol.Resources;
using Autofac;
using PKISharp.WACS.Clients.Acme;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins;
using PKISharp.WACS.Plugins.Base;
using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Plugins.Interfaces;
using System;

namespace PKISharp.WACS.Context
{
    public class ValidationContextParameters(
        AuthorizationContext authorization,
        TargetPart targetPart,
        ValidationPluginOptions options,
        Plugin plugin)
    {
        public OrderContext OrderContext { get; } = authorization.Order;
        public ValidationPluginOptions Options { get; } = options;
        public TargetPart TargetPart { get; } = targetPart;
        public AcmeAuthorization Authorization { get; } = authorization.Authorization;
        public string Label { get; } = authorization.Label;
        public string Name { get; } = plugin.Name;
    }

    public class ValidationContext
    {
        public ValidationContext(
            ILifetimeScope scope,
            ValidationContextParameters parameters)
        {
            if (parameters.Authorization.Identifier == null)
            {
                throw new Exception();
            }
            Identifier = parameters.Authorization.Identifier.Value;
            Label = parameters.Label;
            TargetPart = parameters.TargetPart;
            Authorization = parameters.Authorization;
            OrderResult = parameters.OrderContext.OrderResult;
            Scope = scope;
            PluginName = parameters.Name;
            var backend = scope.Resolve<PluginBackend<IValidationPlugin, IValidationPluginCapability, ValidationPluginOptions>>();
            ValidationPlugin = backend.Backend;
            ChallengeType = backend.Capability.ChallengeType;
            Valid = parameters.Authorization.Status == AcmeClient.AuthorizationValid;
        }
        public bool Valid { get; }
        public ILifetimeScope Scope { get; }
        public string Identifier { get; }
        public string Label { get; }
        public string ChallengeType { get; }
        public string PluginName { get; }
        public OrderResult OrderResult { get; }
        public TargetPart? TargetPart { get; }
        public AcmeAuthorization Authorization { get; set; }
        public AcmeChallenge? Challenge { get; set; }
        public IChallengeValidationDetails? ChallengeDetails { get; set; }
        public IValidationPlugin ValidationPlugin { get; set; }
    }

}
