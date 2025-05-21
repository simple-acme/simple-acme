﻿using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Pkcs;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Target = PKISharp.WACS.DomainObjects.Target;
using X509Extension = Org.BouncyCastle.Asn1.X509.X509Extension;

namespace PKISharp.WACS.Plugins.CsrPlugins
{
    /// <summary>
    /// Common implementation between RSA and EC certificates
    /// </summary>
    public abstract class CsrPlugin<TOptions>(ILogService log, ISettings settings, TOptions options) : 
        ICsrPlugin
        where TOptions : CsrPluginOptions
    {
        protected readonly ILogService _log = log;
        protected readonly ISettings _settings = settings;
        protected readonly TOptions _options = options;

        protected string? _cacheData;
        private AsymmetricCipherKeyPair? _keyPair;

        async Task<Pkcs10CertificationRequest> ICsrPlugin.GenerateCsr(Target target, string? keyPath)
        {
            var identifiers = target.GetIdentifiers(false);
            var commonName = target.CommonName;
            var extensions = new Dictionary<DerObjectIdentifier, X509Extension>();

            if (!string.IsNullOrEmpty(keyPath))
            {
                LoadFromCache(keyPath);
            }

            var dn = CommonName(commonName);
            var keys = await GetKeys();
            ProcessMustStaple(extensions);
            CsrPlugin<TOptions>.ProcessSan(identifiers, extensions);

            var csr = new Pkcs10CertificationRequest(
                new Asn1SignatureFactory(GetSignatureAlgorithm(), keys.Private),
                dn,
                keys.Public,
                new DerSet(new AttributePkcs(
                    PkcsObjectIdentifiers.Pkcs9AtExtensionRequest,
                    new DerSet(new X509Extensions(extensions)))));

            if (!string.IsNullOrEmpty(keyPath))
            {
                await SaveToCache(keyPath);
            }

            return csr;
        }

        /// <summary>
        /// Load cached key information from disk, if needed
        /// </summary>
        /// <param name="cachePath"></param>
        private void LoadFromCache(string cachePath)
        {
            try
            {
                var fi = new FileInfo(cachePath);
                if (fi.Exists)
                {
                    var rawData = new ProtectedString(File.ReadAllText(cachePath), _log);
                    if (!rawData.Error)
                    {
                        _cacheData = rawData.Value;
                        _log.Debug("Re-using private key generated at {time}", fi.LastWriteTime);
                    }
                    else
                    {
                        _log.Warning("Private key at {cachePath} cannot be decrypted, creating new key...", cachePath);
                    }
                }
                else
                {
                    _log.Debug("Creating new private key at {cachePath}...", cachePath);
                }
            }
            catch
            {
                throw new Exception($"Unable to read from cache file {cachePath}");
            }
        }

        /// <summary>
        /// Save cached key information to disk, if needed
        /// </summary>
        /// <param name="cachePath"></param>
        private async Task SaveToCache(string cachePath)
        {
            var rawData = new ProtectedString(_cacheData);
            await FileInfoExtensions.SafeWrite(cachePath, rawData.DiskValue(_settings.Security.EncryptConfig));
        }

        public abstract string GetSignatureAlgorithm();

        /// <summary>
        /// Get public and private keys
        /// </summary>
        /// <returns></returns>
        public Task<AsymmetricCipherKeyPair> GetKeys()
        {
            if (_keyPair == null)
            {
                if (_cacheData == null)
                {
                    _keyPair = GenerateNewKeyPair();
                    _cacheData = PemService.GetPem(_keyPair);
                }
                else
                {
                    try
                    {
                        _keyPair = PemService.ParsePem<AsymmetricCipherKeyPair>(_cacheData);
                        if (_keyPair == null)
                        {
                            throw new InvalidDataException("key");
                        }
                    }
                    catch
                    {
                        _log.Error($"Unable to read cache data, creating new key...");
                        _cacheData = null;
                        return GetKeys();
                    }
                }
            }
            return Task.FromResult(_keyPair);
        }

        /// <summary>
        /// Generate new public/private key pair
        /// </summary>
        /// <returns></returns>
        internal abstract AsymmetricCipherKeyPair GenerateNewKeyPair();

        /// <summary>
        /// Add SAN list
        /// </summary>
        /// <param name="identifiers"></param>
        /// <param name="extensions"></param>
        private static void ProcessSan(List<Identifier> identifiers, Dictionary<DerObjectIdentifier, X509Extension> extensions)
        {
            // SAN
            var names = new GeneralNames(identifiers.
                Select(n => new GeneralName(
                    n.Type switch
                    {
                        IdentifierType.DnsName => GeneralName.DnsName,
                        IdentifierType.IpAddress => GeneralName.IPAddress,
                        _ => GeneralName.OtherName
                    }, 
                    n.Value)).
                ToArray());
            Asn1OctetString asn1ost = new DerOctetString(names);
            extensions.Add(X509Extensions.SubjectAlternativeName, new X509Extension(false, asn1ost));
        }

        /// <summary>
        /// Optionally add the OCSP Must-Stable extension
        /// </summary>
        /// <param name="extensions"></param>
        private void ProcessMustStaple(Dictionary<DerObjectIdentifier, X509Extension> extensions)
        {
            // OCSP Must-Staple
            if (_options.OcspMustStaple == true)
            {
                _log.Information("Enable OCSP Must-Staple extension");
                extensions.Add(
                    new DerObjectIdentifier("1.3.6.1.5.5.7.1.24"),
                    new X509Extension(
                        false,
                        new DerOctetString(
                        [
                            0x30, 0x03, 0x02, 0x01, 0x05
                        ])));
            }
        }

        /// <summary>
        /// Determine the common name 
        /// </summary>
        /// <param name="commonName"></param>
        /// <param name="identifiers"></param>
        /// <returns></returns>
        private static X509Name CommonName(Identifier? commonName)
        {
            var attrs = new Dictionary<DerObjectIdentifier, string?>
            {
                [X509Name.CN] = commonName?.Unicode(false).Value ?? ""
            };
            var ord = new List<DerObjectIdentifier>
            {
                X509Name.CN
            };
            var issuerDN = new X509Name(ord, attrs);
            return issuerDN;
        }
    }
}
