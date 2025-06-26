using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    /// <summary>
    /// Read only
    /// </summary>
    public interface ISecretProvider
    {
        /// <summary>
        /// Make references to this provider unique from 
        /// references in other providers
        /// </summary>
        string Prefix { get; }

        /// <summary>
        /// Get a secret from the vault
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        Task<string?> GetSecret(string? key);
    }

    /// <summary>
    /// Read-write
    /// </summary>
    public interface ISecretService : ISecretProvider
    {
        /// <summary>
        /// (Re)save to disk to support encrypt/decrypt operations
        /// </summary>
        Task Encrypt();

        /// <summary>
        /// List available keys in the system
        /// </summary>
        Task<IEnumerable<string>> ListKeys();

        /// <summary>
        /// Put a secret in the vault
        /// </summary>
        /// <param name="key"></param>
        /// <param name="secret"></param>
        Task PutSecret(string key, string secret);

        /// <summary>
        /// Delete secret from the store
        /// </summary>
        /// <param name="key"></param>
        Task DeleteSecret(string key);
    }
}
