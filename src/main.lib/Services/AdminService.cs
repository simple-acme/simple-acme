using System;
using System.Security.Principal;

namespace PKISharp.WACS.Services
{
    public class AdminService
    {
        private readonly Lazy<bool> _isAdminLazy;
        private readonly Lazy<bool> _isSystemLazy;

        public AdminService() : this(
            () => Environment.IsPrivilegedProcess,
            () =>
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
            })
        {
        }

        internal AdminService(Func<bool> determineAdmin, Func<bool> determineSystem)
        {
            _isAdminLazy = new(determineAdmin);
            _isSystemLazy = new(determineSystem);
        }

        public bool IsAdmin => IsAdminLazy.Value;
        public bool IsSystem => IsSystemLazy.Value;

        private Lazy<bool> IsAdminLazy => _isAdminLazy;
        private Lazy<bool> IsSystemLazy => _isSystemLazy;
    }
}
