using System;
using System.Security.Principal;

namespace PKISharp.WACS.Services
{
    public class AdminService
    {
        public bool IsAdmin => IsAdminLazy.Value;
        public bool IsSystem => IsSystemLazy.Value;

        private Lazy<bool> IsAdminLazy => new(DetermineAdmin);
        private Lazy<bool> IsSystemLazy => new(DetermineSystem);

        private bool DetermineAdmin() => Environment.IsPrivilegedProcess;

        private bool DetermineSystem()
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }
            try
            {
                return WindowsIdentity.GetCurrent().IsSystem;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
