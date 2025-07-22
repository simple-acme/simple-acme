using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Pkcs;
using PKISharp.WACS.Clients;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.TargetPlugins
{
    [IPlugin.Plugin1<
        CsrOptions, CsrOptionsFactory, 
        DefaultCapability, WacsJsonPlugins, CsrArguments>
        ("5C3DB0FB-840B-469F-B5A7-0635D8E9A93D", 
        CsrOptions.Trigger, "CSR created by another program", 
        Name = "Custom CSR")]
    internal class Csr(ILogService logService, CsrOptions options, ScriptClient scriptClient) : ITargetPlugin
    {
        /// <summary>
        /// Generate a Target based on a CSR created by a third party program
        /// either as a static file, or dynamically using a script
        /// </summary>
        /// <returns></returns>
        public async Task<Target?> Generate()
        {
            // Get raw CSR data
            var csrString = "";
            try
            {
                csrString = await GetCsrString();
            } 
            catch (Exception ex)
            {
                logService.Error(ex, "Unable to retrieve raw CSR");
                return null;
            }
 
            // Parse CSR data and extract identifiers
            List<Identifier> alternativeNames;
            Identifier commonName;
            byte[] csrBytes;
            try
            {
                var pem = PemService.ParsePem<Pkcs10CertificationRequest>(csrString) ?? throw new Exception("Unable to construct Pkcs10CertificationRequest");
                var info = pem.GetCertificationRequestInfo();
                csrBytes = pem.GetEncoded();
                commonName = ParseCn(info);
                alternativeNames = [.. ParseSan(info)];
                if (!alternativeNames.Contains(commonName))
                {
                    alternativeNames.Add(commonName);
                }
            }
            catch (Exception ex)
            {
                logService.Error(ex, "Unable to parse CSR");
                return null;
            }

            // Create target
            var ret = new Target($"[{nameof(Csr)}] {options.CsrFile}", commonName, [new(alternativeNames)])
            {
                UserCsrBytes = csrBytes
            };

            // Read and parse PK
            if (!string.IsNullOrWhiteSpace(options.PkFile))
            {
                string pkString;
                try
                {
                    pkString = await File.ReadAllTextAsync(options.PkFile);
                }
                catch (Exception ex)
                {
                    logService.Error(ex, "Unable to read private key from {PkFile}", options.PkFile);
                    return null;
                }
                ret.PrivateKey = ParsePk(pkString);
            }
            return ret;
        }

        /// <summary>
        /// Get PEM encoded CSR from either of the supported sources
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private async Task<string> GetCsrString()
        {
            // Read CSR
            if (!string.IsNullOrEmpty(options.CsrFile))
            {
                var ret = await File.ReadAllTextAsync(options.CsrFile);
                if (string.IsNullOrWhiteSpace(ret))
                {
                    throw new InvalidOperationException($"File {options.CsrFile} was empty");
                }
                return ret;
            }

            // Get CSR from script
            if (!string.IsNullOrEmpty(options.CsrScript))
            {
                var ret = await scriptClient.RunScript(options.CsrScript);
                if (!ret.Success)
                {
                    throw new InvalidOperationException($"Script {options.CsrScript} unable to run");
                }
                if (string.IsNullOrWhiteSpace(ret.Output))
                {
                    throw new InvalidOperationException($"Script {options.CsrScript} output was empty");
                }
                return ret.Output;
            }

            // Misconfiguration
            throw new InvalidOperationException($"Neither CsrFile nor CsrScript are configured");
        }

        /// <summary>
        /// Extract private key from PEM encoded file
        /// </summary>
        /// <param name="pkString"></param>
        /// <returns></returns>
        private AsymmetricKeyParameter? ParsePk(string pkString)
        {
            try
            {
                var keyPair = PemService.ParsePem<AsymmetricCipherKeyPair>(pkString);
                var pkBytes = keyPair != null ?
                    keyPair.Private :
                    PemService.ParsePem<AsymmetricKeyParameter>(pkString);
                return pkBytes ?? throw new Exception("No private key found");
            }
            catch (Exception ex)
            {
                logService.Error(ex, "Unable to parse private key");
                return null;
            }
        }

        /// <summary>
        /// Get the common name
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        private static Identifier ParseCn(CertificationRequestInfo info)
        {
            var subject = info.Subject;
            var cnValue = subject.GetValueList(new DerObjectIdentifier("2.5.4.3"));
            if (cnValue.Count > 0)
            {
                var name = cnValue.Cast<string>().ElementAt(0);
                return new DnsIdentifier(name).Unicode(true);
            } 
            else
            {
                throw new Exception("Unable to parse common name");
            }
        }

        /// <summary>
        /// Parse the SAN names.
        /// Based on https://stackoverflow.com/questions/44824897/getting-subject-alternate-names-with-pkcs10certificationrequest
        /// </summary>
        /// <param name="info"></param>
        /// <returns></returns>
        private IEnumerable<Identifier> ParseSan(CertificationRequestInfo info)
        {
            var ret = new List<Identifier>();
            var extensionSequence = info.Attributes.OfType<DerSequence>()
                .Where(o => o.OfType<DerObjectIdentifier>().Any(oo => oo.Id == "1.2.840.113549.1.9.14"))
                .FirstOrDefault();
            if (extensionSequence == null)
            {
                return ret;
            }
            var extensionSet = extensionSequence.OfType<DerSet>().FirstOrDefault();
            if (extensionSet == null)
            {
                return ret;
            }
            var sequence = extensionSet.OfType<DerSequence>().FirstOrDefault();
            if (sequence == null)
            {
                return ret;
            }
            var derOctetString = GetAsn1ObjectRecursive<DerOctetString>(sequence, "2.5.29.17");
            if (derOctetString == null)
            {
                return ret;
            }
            var asn1object = Asn1Object.FromByteArray(derOctetString.GetOctets());
            var names = Org.BouncyCastle.Asn1.X509.GeneralNames.GetInstance(asn1object);
            return names.GetNames().Select(x => x.TagNo switch {
                1 => new EmailIdentifier(x.Name.ToString()!),
                2 => new DnsIdentifier(x.Name.ToString()!).Unicode(true),
                7 => new IpIdentifier(x.Name.ToString()!),
                _ => new UnknownIdentifier(x.Name.ToString()!)
            });
        }

        /// <summary>
        /// Helper method to parse ANS1
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sequence"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        private T? GetAsn1ObjectRecursive<T>(DerSequence sequence, string id) where T : Asn1Object
        {
            if (sequence.OfType<DerObjectIdentifier>().Any(o => o.Id == id))
            {
                return sequence.OfType<T>().First();
            }
            foreach (var subSequence in sequence.OfType<DerSequence>())
            {
                var value = GetAsn1ObjectRecursive<T>(subSequence, id);
                if (value != default(T))
                {
                    return value;
                }
            }
            return default;
        }
    }
}