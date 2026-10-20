using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.OrderPlugins
{
    internal class HostCapability(Target target) : DefaultCapability
    {
        protected readonly Target Target = target;

        public override Task<State> ExecutionState()
        {
            return Target.UserCsrBytes == null ? 
                Task.FromResult(State.EnabledState()) : 
                Task.FromResult(State.DisabledState("Renewals sourced from a custom CSR cannot be split up"));
        }
    }
}
