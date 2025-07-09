using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System.Linq;

namespace PKISharp.WACS.Plugins.TargetPlugins
{
    internal class IISCapability(IUserRoleService userRole, IIISClient iisClient) : DefaultCapability
    {
        public override State ExecutionState
        {
            get
            {
                var state = userRole.IISState;
                if (state.Disabled)
                {
                    return state;
                }
                if (!iisClient.Sites.Any())
                {
                    return State.DisabledState("No IIS sites detected.");
                }
                return State.EnabledState();
            }
        }
    }
}
