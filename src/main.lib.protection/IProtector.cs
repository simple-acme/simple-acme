namespace PKISharp.WACS.Services
{
    /// <summary>
    /// Service that handles encryption and decryption of sensitive values such as passwords.
    /// </summary>
    public interface IProtector
    {
        /// <summary>
        /// Convert clear text to encrypted text
        /// </summary>
        /// <param name="clearText">Clear bytes to protect.</param>
        /// <returns>Encrypted bytes.</returns>
        public byte[] Protect(byte[] clearText);

        /// <summary>
        /// Convert encrypted text back to clear text
        /// </summary>
        /// <param name="encryptedText">Encrypted bytes to unprotect.</param>
        /// <returns>Clear text bytes.</returns>
        public byte[] Unprotect(byte[] encryptedText);
    }
}
