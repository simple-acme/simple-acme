using Org.BouncyCastle.Asn1.Nist;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using System;
using System.IO;

namespace PKISharp.WACS.Services
{
    /// <summary>
    /// Wrapper class to keep track of configured protection mode
    /// </summary>
    public class PfxWrapper(Pkcs12Store store, PfxProtectionMode protectionMode)
    {
        public Pkcs12Store Store { get; private set; } = store;
        public PfxProtectionMode ProtectionMode { get; private set; } = protectionMode;
    }

    /// <summary>
    /// Available protection modes
    /// </summary>
    public enum PfxProtectionMode
    {
        Default,
        Legacy,
        Aes256
    }

    public class PfxService
    {
        /// <summary>
        /// Helper function to create a new PfxWrapper with certain settings
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static PfxWrapper GetPfx(PfxProtectionMode protectionMode)
        {
            var outputBuilder = new Pkcs12StoreBuilder();

            // Prevent later warnings about RejectDuplicateAttributes
            // pending either
            // https://github.com/dotnet/runtime/issues/113726
            // or
            // https://github.com/bcgit/bc-csharp/issues/605
            // to be resolved
            outputBuilder.SetEnableOracleTrustedKeyUsage(false);

            if (protectionMode == PfxProtectionMode.Default) 
            {
                if (OperatingSystem.IsWindows())
                {
                    // Windows 10 version 1809 / Server 2019 and above support AES-256
                    // Older versions only support RC2-40
                    protectionMode =
                        Environment.OSVersion.Version.Build >= 17763 ?
                        PfxProtectionMode.Aes256 :
                        PfxProtectionMode.Legacy;
                }
                else
                {
                    // AES-256 default for other platforms
                    protectionMode = PfxProtectionMode.Aes256;
                }

            }
            if (protectionMode == PfxProtectionMode.Aes256)
            {
                outputBuilder.SetKeyAlgorithm(
                    NistObjectIdentifiers.IdAes256Cbc,
                    PkcsObjectIdentifiers.IdHmacWithSha256);
            }
            return new PfxWrapper(outputBuilder.Build(), protectionMode);
        }

        /// <summary>
        /// Helper function to create a new PfxWrapper with different PfxProtectionMode
        /// setting
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static PfxWrapper ConvertPfx(PfxWrapper original, PfxProtectionMode protection)
        {
            if (original.ProtectionMode == protection) 
            {
                return original;
            }
            var stream = new MemoryStream();
            var password = PasswordGenerator.Generate().ToCharArray();
            original.Store.Save(stream, password, new SecureRandom());
            stream.Seek(0, SeekOrigin.Begin);
            var ret = GetPfx(protection);
            ret.Store.Load(stream, password);
            return ret;
        }
    }
}
