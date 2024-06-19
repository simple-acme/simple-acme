using ACMESharp.Protocol;

namespace PKISharp.WACS.Clients.Acme
{
    /// <summary>
    /// Constructor requires signer to be present
    /// </summary>
    /// <param name="signer"></param>
    internal class Account(AccountDetails details, AccountSigner signer)
    {

        /// <summary>
        /// Account information
        /// </summary>
        public AccountDetails Details { get; set; } = details;

        /// <summary>
        /// Account "password"
        /// </summary>
        public AccountSigner Signer { get; set; } = signer;
    }
}
