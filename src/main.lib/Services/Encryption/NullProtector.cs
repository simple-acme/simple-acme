using System;

namespace PKISharp.WACS.Services.Encryption
{
    /// <summary>
    /// Fallback implementation that doesn't do anything
    /// </summary>
    internal class NullProtector : IProtector
    {
        public byte[] Protect(byte[] clearText) => throw new InvalidOperationException();
        public byte[] Unprotect(byte[] encryptedText) => throw new InvalidOperationException();
    }
}
