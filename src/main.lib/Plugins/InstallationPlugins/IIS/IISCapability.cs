using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.StorePlugins;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.InstallationPlugins
{
    internal class IISCapability(IUserRoleService userRole, IIISClient iisClient) : InstallationCapability
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

        public override State CanInstall(IEnumerable<Type> storeTypes, IEnumerable<Type> installationTypes)
        {
            if (installationTypes.Contains(typeof(IIS)))
            {
                return State.DisabledState("Cannot be used more than once in a renewal.");
            }
            if (storeTypes.Contains(typeof(CertificateStore)) || storeTypes.Contains(typeof(CentralSsl)))
            {
                return State.EnabledState();
            }
            return State.DisabledState("Requires CertificateStore or CentralSsl store plugin.");
        }
    }
}
