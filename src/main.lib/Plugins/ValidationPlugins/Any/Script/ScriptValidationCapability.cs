using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using System.Collections.Generic;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Any
{
    class ScriptValidationCapability(Target target, ScriptOptions? options = null) : AnyValidationCapability(target) 
    {
        public override IEnumerable<string> ChallengeTypes => [Constants.Dns01ChallengeType, Constants.Http01ChallengeType];
        public override State ExecutionState => options?.ChallengeType == Constants.Http01ChallengeType ?
            new HttpValidationCapability(Target).ExecutionState :
            new DnsValidationCapability(Target).ExecutionState;
    }
}
