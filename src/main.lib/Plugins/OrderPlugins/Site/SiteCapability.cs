using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.OrderPlugins
{
    internal class SiteCapability(Target target) : DefaultCapability
    {
        protected readonly Target Target = target;

        public override Task<State> ExecutionState()
        {
            if (Target.UserCsrBytes != null)
            {
                return Task.FromResult(State.DisabledState("Renewals sourced from a custom CSR cannot be split up"));
            }
            if (!Target.Parts.Any(p => p.SiteId > 0))
            {
                return Task.FromResult(State.DisabledState("No site information included in source"));
            }
            return Task.FromResult(State.EnabledState());
        }
    }
}
