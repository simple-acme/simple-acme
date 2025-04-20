using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Interfaces;
using System.Collections.Generic;
using System.Linq;

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
        public override State State => 
            Target.GetIdentifiers(false).Any(x => x.Value.StartsWith("*.")) ? 
            State.DisabledState("HTTP validation cannot be used for wildcard identifiers (e.g. *.example.com)") : 
            State.EnabledState();
    }

    public class DnsValidationCapability(Target target) : ValidationCapability
    {
        protected readonly Target Target = target;
        public override IEnumerable<string> ChallengeTypes => [Constants.Dns01ChallengeType];
        public override State State =>
            !Target.Parts.SelectMany(x => x.Identifiers).All(x => x.Type == IdentifierType.DnsName) ?
            State.DisabledState("DNS validation can only be used for DNS identifiers") :
            State.EnabledState();
    }

    public class TlsValidationCapability(Target target) : ValidationCapability
    {
        protected readonly Target Target = target;
        public override IEnumerable<string> ChallengeTypes => [Constants.TlsAlpn01ChallengeType];
        public override State State =>
            Target.GetIdentifiers(false).Any(x => x.Value.StartsWith("*.")) ?
            State.DisabledState("TLS-ALPN validation cannot be used for wildcard identifiers (e.g. *.example.com)") :
            State.EnabledState();
    }

    public class AnyValidationCapability(Target target) : ValidationCapability
    {
        protected readonly Target Target = target;
        public override IEnumerable<string> ChallengeTypes => [Constants.Dns01ChallengeType, Constants.Http01ChallengeType, Constants.TlsAlpn01ChallengeType];
        public override State State => State.EnabledState();
    }
}
