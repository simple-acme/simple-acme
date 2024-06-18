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

        private bool DetermineAdmin()
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }
            bool isAdmin;
            WindowsIdentity? user = null;
            try
            {
                //get the currently logged in user
                user = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(user);
                isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (UnauthorizedAccessException)
            {
                isAdmin = false;
            }
            catch (Exception)
            {
                isAdmin = false;
            }
            finally
            {
                user?.Dispose();
            }
            return isAdmin;
        }

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
