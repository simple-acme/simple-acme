using System;

namespace PKISharp.WACS.Services.Encryption
{
    /// <summary>
    /// Fallback implementation that doesn't do anything
    /// </summary>
    internal class NullProtector : IProtector
    {
        public byte[] Protect(byte[] clearText) => throw new InvalidOperationException("Configuration encryption is not supported on this platform. Disable Security.EncryptConfig in settings.json.");
        public byte[] Unprotect(byte[] encryptedText) => throw new InvalidOperationException("Configuration encryption is not supported on this platform. Disable Security.EncryptConfig in settings.json.");
    }
}
