using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    internal class UserStoreCapability(AdminService adminService) : DefaultCapability
    {
        public override Task<State> ExecutionState() =>
            adminService.IsSystem ?
            Task.FromResult(State.DisabledState("It doesn't make sense to use the user store plugin while running as SYSTEM.")) :
            Task.FromResult(State.EnabledState());
    }
}
