using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base.Capabilities;
using System.Collections.Generic;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Any
{
    class ManualValidationCapability(Target target) : AnyValidationCapability(target)
    {
        public override IEnumerable<string> ChallengeTypes => [Constants.Dns01ChallengeType, Constants.Http01ChallengeType];
    }
}
