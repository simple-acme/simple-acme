using ACMESharp.Authorizations;
using Org.BouncyCastle.Asn1;
using PKISharp.WACS.Context;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Tls
{
    [IPlugin.Plugin<
        SelfHostingOptions, SelfHostingOptionsFactory, 
        SelfHostingCapability, WacsJsonPlugins>
        ("a1565064-b208-4467-8ca1-1bd3c08aa500", 
        "SelfHosting", "Answer TLS verification request from win-acme")]
    internal class SelfHosting(ILogService log, SelfHostingOptions options) : Validation<TlsAlpn01ChallengeValidationDetails>
    {
        internal const int DefaultValidationPort = 443;
        private TcpListener? _listener;
        private CancellationTokenSource? _tokenSource;
        private X509Certificate2? _certificate;

        public async Task RecieveRequests()
        {
            if (_tokenSource == null || _listener == null)
            {
                throw new InvalidOperationException();
            }
            while (!_tokenSource.Token.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync();
                using var sslStream = new SslStream(client.GetStream());
                var sslOptions = new SslServerAuthenticationOptions
                {
                    ApplicationProtocols = [new("acme-tls/1")],
                    ServerCertificate = _certificate
                };
                sslStream.AuthenticateAsServer(sslOptions);
                sslStream.Flush();
                client.Close();
            }
        }

        public override Task Commit() => Task.CompletedTask;

        public override Task CleanUp()
        {
            _tokenSource?.Cancel();
            _listener?.Stop();
            _listener = null;
            return Task.CompletedTask;
        }

        public static (TcpListener, int) CreateListener(int? userPort)
        {
            var port = userPort ?? DefaultValidationPort;
            var testListener = new TcpListener(IPAddress.IPv6Any, port);
            testListener.Server.DualMode = true;
            if (OperatingSystem.IsWindows())
            {
                testListener.AllowNatTraversal(true);
            }
            return (testListener, port);
        }

        public override Task PrepareChallenge(ValidationContext context, TlsAlpn01ChallengeValidationDetails challenge)
        {
            var port = DefaultValidationPort;
            try
            {
                using var rsa = RSA.Create(2048);
                var name = new X500DistinguishedName($"CN={context.Identifier}");

                var request = new CertificateRequest(
                    name,
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(challenge.TokenValue));
                request.CertificateExtensions.Add(
                    new X509Extension(
                        new AsnEncodedData("1.3.6.1.5.5.7.1.31",
                            new DerOctetString(hash).GetDerEncoded()),
                            true));

                var sanBuilder = new SubjectAlternativeNameBuilder();
                sanBuilder.AddDnsName(context.Identifier);
                request.CertificateExtensions.Add(sanBuilder.Build());

                _certificate = request.CreateSelfSigned(
                    new DateTimeOffset(DateTime.UtcNow.AddDays(-1)),
                    new DateTimeOffset(DateTime.UtcNow.AddDays(1)));

                _certificate = new X509Certificate2(
                    _certificate.Export(X509ContentType.Pfx, context.Identifier),
                    context.Identifier,
                    X509KeyStorageFlags.MachineKeySet);

                _tokenSource = new();

                var (newListener, newPort) = CreateListener(options.Port);
                port = newPort;
                newListener.Start();
                _listener = newListener;
                Task.Run(RecieveRequests);
            }
            catch
            {
                log.Error("Unable to activate TcpClient for port {port}", port);
                throw;
            }
            return Task.CompletedTask;
        }
    }
}
