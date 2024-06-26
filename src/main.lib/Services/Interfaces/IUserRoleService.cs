using PKISharp.WACS.Plugins.Interfaces;

namespace PKISharp.WACS.Services
{
    public interface IUserRoleService
    {
        bool AllowCertificateStore { get; }
        State IISState { get; }
        bool AllowAutoRenew { get; }
    }
}
