using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace PKISharp.WACS.Services.Encryption
{
    /// <summary>
    /// Fallback implementation that doesn't do anything
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal class DpApiProtector : IProtector
    {
        public byte[] Protect(byte[] clearText) => ProtectedData.Protect(clearText, null, DataProtectionScope.LocalMachine);
        public byte[] Unprotect(byte[] encryptedText) => ProtectedData.Unprotect(encryptedText, null, DataProtectionScope.LocalMachine);
    }
}
