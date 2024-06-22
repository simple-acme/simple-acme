using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    internal interface IAutoRenewService
    {
        bool ConfirmAutoRenew();
        Task SetupAutoRenew(RunLevel runLevel);
        Task EnsureAutoRenew(RunLevel runLevel);
    }
}