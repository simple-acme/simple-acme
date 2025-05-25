﻿using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;

namespace PKISharp.WACS.Plugins.CsrPlugins
{
    [IPlugin.Plugin1<
        EcOptions, CsrPluginOptionsFactory<EcOptions, EcArguments>, 
        DefaultCapability, WacsJsonPlugins, EcArguments>
        ("9aadcf71-5241-4c4f-aee1-bfe3f6be3489", 
        "EC", "Generate an EC public/private key pair", 
        Name = "Elliptic Curve")]
    internal class Ec(
        ILogService log,
        ISettings settings,
        EcOptions options) : CsrPlugin<EcOptions>(log, settings, options)
    {
        internal override AsymmetricCipherKeyPair GenerateNewKeyPair()
        {
            var generator = new ECKeyPairGenerator();
            var curve = GetEcCurve();
            _log.Verbose("Generating private key using curve {curve}", curve);
            var genParam = new ECKeyGenerationParameters(
                SecNamedCurves.GetOid(curve),
                new SecureRandom());
            generator.Init(genParam);
            return generator.GenerateKeyPair();
        }

        /// <summary>
        /// Parameters to generate the key for
        /// </summary>
        /// <returns></returns>
        private string GetEcCurve()
        {
            var ret = "secp384r1"; // Default
            try
            {
                var config = _settings.Csr.Ec.CurveName;
                if (!string.IsNullOrWhiteSpace(""))
                {
                    DerObjectIdentifier? curveOid = null;
                    try
                    {
                        curveOid = SecNamedCurves.GetOid(config);
                    }
                    catch { }
                    if (curveOid != null)
                    {
                        ret = config;
                    }
                    else
                    {
                        _log.Warning("Unknown curve {ECCurve}", config);
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Unable to get EC name");
            }
            _log.Debug("ECCurve: {ECCurve}", ret);
            return ret;
        }

        public override string GetSignatureAlgorithm() => _settings.Csr.Ec.SignatureAlgorithm;
    }
}
