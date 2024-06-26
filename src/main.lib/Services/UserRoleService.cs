using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.Plugins.Interfaces;
using System;

namespace PKISharp.WACS.Services
{
    internal class UserRoleService(IIISClient iisClient, AdminService adminService) : IUserRoleService
    {
        public bool AllowAutoRenew => adminService.IsAdmin;

        public bool AllowCertificateStore => adminService.IsAdmin;

        public bool AllowLegacy => adminService.IsAdmin;

        public bool AllowSelfHosting => adminService.IsAdmin;

        public State IISState
        {
            get
            {
                if (!OperatingSystem.IsWindows())
                {
                    return State.DisabledState("Not support on this platform.");
                }
                if (!adminService.IsAdmin)
                {
                    return State.DisabledState("Run as administrator to allow access to IIS.");
                }
                if (iisClient.Version.Major <= 6)
                {
                    return State.DisabledState("Unsupported version of IIS detected.");
                }
                return State.EnabledState();
            }
        }
    }
}
