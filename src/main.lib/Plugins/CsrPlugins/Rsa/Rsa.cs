using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Security;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;

namespace PKISharp.WACS.Plugins.CsrPlugins
{
    [IPlugin.Plugin1<
        RsaOptions, CsrPluginOptionsFactory<RsaOptions, RsaArguments>,
        DefaultCapability, WacsJsonPlugins, RsaArguments>
        ("b9060d4b-c2d3-49ac-b37f-962e7c3cbe9d", 
        "RSA", "Generate an RSA public/private key pair")]
    internal class Rsa(
        ILogService log,
        ISettings settings,
        RsaOptions options) : CsrPlugin<RsaOptions>(log, settings, options)
    {

        /// <summary>
        /// Generate new RSA key pair
        /// </summary>
        /// <returns></returns>
        internal override AsymmetricCipherKeyPair GenerateNewKeyPair()
        {
            var randomGenerator = new CryptoApiRandomGenerator();
            var random = new SecureRandom(randomGenerator);
            var keyBits = _settings.Csr?.Rsa?.KeyBits ??
#pragma warning disable CS0618
                _settings.Security?.RSAKeyBits ??
#pragma warning restore CS0618
                3072;

            _log.Verbose("Generating private key using {keyBits} key bits", keyBits);
            var keyGenerationParameters = new KeyGenerationParameters(random, keyBits);
            var keyPairGenerator = new RsaKeyPairGenerator();
            keyPairGenerator.Init(keyGenerationParameters);
            var subjectKeyPair = keyPairGenerator.GenerateKeyPair();
            return subjectKeyPair;
        }

        public override string GetSignatureAlgorithm() => 
            _settings.Csr?.Rsa?.SignatureAlgorithm ?? "SHA512withRSA";
    }
}
