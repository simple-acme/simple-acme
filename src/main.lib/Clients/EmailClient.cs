using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace PKISharp.WACS.Clients
{
    internal class EmailClient
    {
        private readonly ILogService _log;

#pragma warning disable
        // Not used, but must be initialized to create settings.json on clean install
        private readonly ISettings _settings;
#pragma warning enable

        private readonly string _server;
        private readonly int _port;
        private readonly string _user;
        private readonly string _password;
        private readonly bool _secure;
        private readonly int? _secureMode;
        private readonly string _senderName;
        private readonly string _senderAddress;
        private readonly string _computerName;
        private readonly string _version;
        private readonly IEnumerable<string> _receiverAddresses;
        private readonly SecretServiceManager _secretService;

        public EmailClient(
            ILogService log, 
            ISettings settings, 
            SecretServiceManager secretService)
        {
            _log = log;
            _settings = settings;
            _server = _settings.Notification.Email.SmtpServer;
            _port = _settings.Notification.Email.SmtpPort;
            _user = _settings.Notification.Email.SmtpUser;
            _password = _settings.Notification.Email.SmtpPassword;
            _secure = _settings.Notification.Email.SmtpSecure;
            _secureMode = _settings.Notification.Email.SmtpSecureMode;
            _senderName = _settings.Notification.Email.SenderName;
            _computerName = _settings.Notification.ComputerName;
            _secretService = secretService;

            if (string.IsNullOrEmpty(_computerName)) {
                _computerName = Environment.MachineName;
            }
            _version = VersionService.SoftwareVersion.ToString();

            if (string.IsNullOrWhiteSpace(_senderName))
            {
                _senderName = _settings.Client.ClientName;
            }
            _senderAddress = _settings.Notification.Email.SenderAddress;
            _receiverAddresses = _settings.Notification.Email.ReceiverAddresses;

            // Criteria for emailing to be enabled at all
            if (string.IsNullOrEmpty(_senderAddress))
            {
                State = State.DisabledState("No sender address configured");
            }
            else if (string.IsNullOrEmpty(_server))
            {
                State = State.DisabledState("No SMTP server configured");
            }
            else if (!_receiverAddresses.Any())
            {
                State = State.DisabledState("No receiver address configured");
            }
            else
            {
                State = State.EnabledState();
            }
        }

        public State State { get; internal set; }

        public async Task<bool> Send(string subject, string content, MessagePriority priority)
        {
            if (!State.Disabled)
            {
                return false;
            }
            using var logStream = new MemoryStream();
            var logger = new ProtocolLogger(logStream);
            using var client = new SmtpClient(logger);
            try
            {
                var options = SecureSocketOptions.None;
                if (_secure)
                {
                    if (_secureMode.HasValue)
                    {
                        switch (_secureMode.Value)
                        {
                            case 1:
                                options = SecureSocketOptions.Auto;
                                break;
                            case 2:
                                options = SecureSocketOptions.SslOnConnect;
                                break;
                            case 3:
                                options = SecureSocketOptions.StartTls;
                                break;
                            case 4:
                                options = SecureSocketOptions.StartTlsWhenAvailable;
                                break;
                        }
                    }
                    else
                    {
                        options = SecureSocketOptions.StartTls;
                    }
                }
                await client.ConnectAsync(_server, _port, options);
                if (!string.IsNullOrEmpty(_user))
                {
                    var evaluatedPassword = await _secretService.EvaluateSecret(_password);
                    await client.AuthenticateAsync(new NetworkCredential(_user, evaluatedPassword));
                }
                foreach (var receiverAddress in _receiverAddresses)
                {
                    _log.Information("Sending e-mail with subject {subject} to {_receiverAddress}", subject, receiverAddress);
                    var sender = new MailboxAddress(_senderName, _senderAddress);
                    var receiver = new MailboxAddress("Receiver", receiverAddress);
                    var message = new MimeMessage()
                    {
                        Sender = sender,
                        Priority = priority,
                        Subject = subject
                    };
                    message.Subject = $"{subject} ({_computerName})";
                    message.From.Add(sender);
                    message.To.Add(receiver);
                    var bodyBuilder = new BodyBuilder();
                    bodyBuilder.HtmlBody = content + $"<p>Sent by {_settings.Client.ClientName} version {_version} from {_computerName}</p>";
                    message.Body = bodyBuilder.ToMessageBody();
                    await client.SendAsync(message);
                }                       
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Problem sending e-mail");
                logStream.Position = 0;
                var logReader = new StreamReader(logStream);
                var logOutput = logReader.ReadToEnd();
                if (!string.IsNullOrWhiteSpace(logOutput))
                {
                    _log.Debug(logOutput);
                }
                return false;
            }
            return true;
        }
    }
}
