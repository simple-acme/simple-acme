using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    internal class CertificateStoreCapability(IUserRoleService userRoleService) : DefaultCapability
    {
        public override State ExecutionState
        {
            get
            {
                if (!OperatingSystem.IsWindows())
                {
                    return State.DisabledState("Not supported on this platform.");
                }
                return userRoleService.AllowCertificateStore ?
                    State.EnabledState() : 
                    State.DisabledState("Run as administrator to allow certificate store access.");
            }
        }
          
    }
}
