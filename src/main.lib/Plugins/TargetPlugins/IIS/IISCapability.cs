using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.TargetPlugins
{
    internal class IISCapability(IUserRoleService userRole, IIISClient iisClient) : DefaultCapability
    {
        public override Task<State> ExecutionState()
        {
            var state = userRole.IISState;
            if (state.Disabled)
            {
                return Task.FromResult(state);
            }
            if (!iisClient.Sites.Any())
            {
                return Task.FromResult(State.DisabledState("No IIS sites detected."));
            }
            return Task.FromResult(State.EnabledState());
        }
    }
}