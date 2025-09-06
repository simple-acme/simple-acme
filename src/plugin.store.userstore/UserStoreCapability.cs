using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    internal class UserStoreCapability(AdminService adminService) : DefaultCapability
    {
        public override State ExecutionState =>
            adminService.IsSystem ?
            State.DisabledState("It doesn't make sense to use the user store plugin while running as SYSTEM.") :
            State.EnabledState();
    }
}
