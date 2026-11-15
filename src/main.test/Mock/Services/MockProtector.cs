using PKISharp.WACS.Services;
using System;
using System.Linq;

namespace PKISharp.WACS.UnitTests.Mock.Services
{
    /// <summary>
    /// Fake encryptin (reverse the bytes) enables 
    /// tests to run on Linux because DpApi is unavailable
    /// </summary>
    internal class MockProtector : IProtector
    {

        public byte[] Protect(byte[] clearText)
        {
            return [.. clearText.Reverse()];
        }

        public byte[] Unprotect(byte[] encryptedText)
        {
            return [.. encryptedText.Reverse()];
        }
    }
}
