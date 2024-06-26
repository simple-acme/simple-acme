using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;

namespace PKISharp.WACS.Plugins.OrderPlugins
{
    internal class HostCapability(Target target) : DefaultCapability
    {
        protected readonly Target Target = target;

        public override State State =>
            Target.UserCsrBytes == null ?
            State.EnabledState() :
            State.DisabledState("Renewals sourced from a custom CSR cannot be split up");
    }
}
