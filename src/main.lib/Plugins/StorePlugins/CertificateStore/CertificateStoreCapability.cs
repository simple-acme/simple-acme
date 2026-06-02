using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    internal class CertificateStoreCapability(IUserRoleService userRoleService) : DefaultCapability
    {
        public override Task<State> ExecutionState()
        {
            if (!OperatingSystem.IsWindows())
            {
                return Task.FromResult(State.DisabledState("Not supported on this platform."));
            }
            return userRoleService.AllowCertificateStore ?
                Task.FromResult(State.EnabledState()) :
                Task.FromResult(State.DisabledState("Run as administrator to allow certificate store access."));
        }
    }
}