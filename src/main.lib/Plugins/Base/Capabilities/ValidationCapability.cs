using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.Base.Capabilities
{
    public abstract class ValidationCapability : DefaultCapability, IValidationPluginCapability
    {
        public abstract IEnumerable<string> ChallengeTypes { get; }
    }

    public class HttpValidationCapability(Target target) : ValidationCapability
    {
        protected readonly Target Target = target;
        public override IEnumerable<string> ChallengeTypes => [Constants.Http01ChallengeType];
        public override Task<State> ExecutionState() => ConfigurationState();
        public override Task<State> ConfigurationState() => 
            Target.GetIdentifiers(false).Any(x => x.Value.StartsWith("*.")) ? 
            Task.FromResult(State.DisabledState("HTTP validation cannot be used for wildcard identifiers (e.g. *.example.com)")) : 
            Task.FromResult(State.EnabledState());
    }

    public class DnsValidationCapability(Target target) : ValidationCapability
    {
        protected readonly Target Target = target;
        public override IEnumerable<string> ChallengeTypes => [Constants.Dns01ChallengeType];
        public override Task<State> ExecutionState() => ConfigurationState();
        public override Task<State> ConfigurationState() =>
            !Target.Parts.SelectMany(x => x.Identifiers).All(x => x.Type == IdentifierType.DnsName) ?
            Task.FromResult(State.DisabledState("DNS validation can only be used for DNS identifiers")) :
            Task.FromResult(State.EnabledState());
    }

    public class TlsValidationCapability(Target target) : ValidationCapability
    {
        protected readonly Target Target = target;
        public override IEnumerable<string> ChallengeTypes => [Constants.TlsAlpn01ChallengeType];
        public override Task<State> ExecutionState() => ConfigurationState();
        public override Task<State> ConfigurationState() =>
            Target.GetIdentifiers(false).Any(x => x.Value.StartsWith("*.")) ?
            Task.FromResult(State.DisabledState("TLS-ALPN validation cannot be used for wildcard identifiers (e.g. *.example.com)")) :
            Task.FromResult(State.EnabledState());
    }

    public class AnyValidationCapability(Target target) : ValidationCapability
    {
        protected readonly Target Target = target;
        public override IEnumerable<string> ChallengeTypes => [Constants.Dns01ChallengeType, Constants.Http01ChallengeType, Constants.TlsAlpn01ChallengeType];
        public override Task<State> ExecutionState() => Task.FromResult(State.EnabledState());
        public override Task<State> ConfigurationState() => Task.FromResult(State.EnabledState());
    }
}
