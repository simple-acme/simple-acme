using System.Text;

namespace PKISharp.WACS.Services.Serialization
{
    /// <summary>
    /// Wrapper to handle string encryption and encoding
    /// Strings can be in three forms:
    /// - Clear, prefixed by ClearPrefix
    /// - Base64 encoded, without any prefix
    /// - Base64 encoded *with* encryption, prefixed by EncryptedPrefix
    /// </summary>
    public class ProtectedString : IEquatable<ProtectedString>, IComparable, IComparable<ProtectedString>
    {
        /// <summary>
        /// Indicates encryption
        /// </summary>
        internal const string EncryptedPrefix = "enc-";

        /// <summary>
        /// Indicates clear text
        /// </summary>
        internal const string ClearPrefix = "clear-";

        /// <summary>
        /// Provides data protection services for protecting and unprotecting values.
        /// </summary>
        private readonly IProtector _protector;

        /// <summary>
        /// Indicates if there was an error decoding or decrypting the string
        /// </summary>
        public bool Error { get; private set; } = false;

        /// <summary>
        /// Clear value, should be used for operations
        /// </summary>
        public string? Value { get; private set; }

        /// <summary>
        /// Value to save to disk, based on the setting
        /// </summary>
        public string? DiskValue(bool encrypt)
        {
            if (string.IsNullOrEmpty(Value) || Error)
            {
                return Value;
            }
            if (Value.StartsWith(Constants.VaultPrefix))
            {
                return Value;
            }
            if (encrypt) 
            {
                return EncryptedPrefix + Protect(Value);
            } 
            else
            {
                return Encode(Value);
            }
        }
        
        /// <summary>
        /// Version of the string safe to be displayed on screen and in logs
        /// </summary>
        /// <returns></returns>
        public string DisplayValue
        {
            get
            {
                if (Value?.StartsWith(Constants.VaultPrefix) ?? false)
                {
                    return Value;
                }
                return "********";
            }
        }

        /// <summary>
        /// Constructor for user input, always starting with clear text
        /// </summary>
        /// <param name="clearValue"></param>
        internal ProtectedString(string? clearValue, IProtector protector)
        {
            _protector = protector;
            Value = clearValue;
        }

        /// <summary>
        /// Constructor for deserialisation, may be any format
        /// </summary>
        /// <param name="rawValue"></param>
        /// <param name="log"></param>
        /// <param name="protector"></param>
        internal ProtectedString(string rawValue, ILogService log, IProtector protector)
        {
            _protector = protector;
            Value = rawValue;

            if (!string.IsNullOrEmpty(rawValue))
            {
                if (rawValue.StartsWith(EncryptedPrefix))
                {
                    // Sure to be encrypted
                    try
                    {
                        Value = Unprotect(rawValue[EncryptedPrefix.Length..]);
                    }
                    catch
                    {
                        log.Error("Unable to decrypt configuration value, may have been written by a different machine.");
                        Error = true;
                    }
                }
                else if (rawValue.StartsWith(ClearPrefix))
                {
                    // Sure to be clear/unencoded
                    Value = rawValue[ClearPrefix.Length..];
                }
                else if (rawValue.StartsWith(Constants.VaultPrefix)) 
                {
                    // Sure to be clear/unencoded
                    Value = rawValue;
                }
                else
                {
                    // Should be Base64
                    try
                    {
                        var clearBytes = Convert.FromBase64String(rawValue);
                        Value = Encoding.UTF8.GetString(clearBytes);
                    }
                    catch
                    {
                        log.Error("Unable to decode configuration value, use the prefix {prefix} to input clear text", ClearPrefix);
                        Error = true;
                    }
                }
            }
        }

        /// <summary>
        /// Base64 encode a string
        /// </summary>
        /// <param name="clearText"></param>
        /// <returns></returns>
        private static string Encode(string clearText)
        {
            var clearBytes = Encoding.UTF8.GetBytes(clearText);
            return Convert.ToBase64String(clearBytes);
        }

        /// <summary>
        /// Protects the specified text and returns the protected data as a Base64 string.
        /// </summary>
        /// <param name="clearText">Text to protect.</param>
        /// <returns>Base64-encoded protected bytes produced from the UTF-8 representation of the input text.</returns>
        private string Protect(string clearText)
        {
            var clearBytes = Encoding.UTF8.GetBytes(clearText);
            var encryptedBytes = _protector.Protect(clearBytes);
            return Convert.ToBase64String(encryptedBytes);
        }

        /// <summary>
        /// Base64 decode and decrypt a string
        /// </summary>
        /// <param name="encryptedText">Base64-encoded protected text to unprotect.</param>
        /// <returns>The unprotected text.</returns>
        private string? Unprotect(string encryptedText)
        {
            if (encryptedText == null)
            {
                return null;
            }
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var clearBytes = _protector.Unprotect(encryptedBytes);
            return Encoding.UTF8.GetString(clearBytes);
        }

        public override bool Equals(object? obj) => (obj as ProtectedString)?.Value == Value;
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public bool Equals(ProtectedString? other) => other?.Value == Value;
        public int CompareTo(object? obj) => (obj as ProtectedString)?.Value?.CompareTo(Value) ?? -1;
        public int CompareTo(ProtectedString? other) => other?.Value?.CompareTo(Value) ?? -1;
        public static bool operator ==(ProtectedString? a, ProtectedString? b) => a?.Value == b?.Value;
        public static bool operator !=(ProtectedString? a, ProtectedString? b) => a?.Value != b?.Value;
    }
}
