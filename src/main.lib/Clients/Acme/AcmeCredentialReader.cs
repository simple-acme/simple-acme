using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;

namespace PKISharp.WACS.Clients.Acme
{
    public readonly struct EabCredential
    {
        public string Algorithm { get; init; }
        public string KeyIdentifier { get; init; }
        public string Key { get; init; }
    }

    internal class AcmeCredentialReader
    {
        private readonly ILogService _log;
        private readonly IInputService _input;
        private readonly ArgumentsParser _arguments;
        private readonly AccountArguments _accountArguments;
        private readonly SecretServiceManager _secretServiceManager;
        private readonly ZeroSsl _zeroSsl;

        public AcmeCredentialReader(
            IInputService inputService,
            ArgumentsParser arguments,
            ILogService log,
            SecretServiceManager secretServiceManager,
            ZeroSsl zeroSsl)
        {
            _log = log;
            _arguments = arguments;
            _accountArguments = _arguments.GetArguments<AccountArguments>() ?? new AccountArguments();
            _input = inputService;
            _secretServiceManager = secretServiceManager;
            _zeroSsl = zeroSsl;
        }

        /// <summary>
        /// Get contact information
        /// </summary>
        /// <returns></returns>
        public async Task<string[]> GetContacts(RunLevel runLevel, bool zeroSsl = false)
        {
            var allowMultiple = true;
            var allowNone = true;
            var prefix = "mailto:";
            var question = "The ACME server may be able to record one or more email addresses associated with your account " +
                "to be able to send notifications about security incidents and abuse. In some cases providing an email address " +
                "may even be mandatory.";
            if (zeroSsl)
            {
                allowMultiple = false;
                allowNone = false;
                prefix = "";
                question = "Please provide an email address to be associated with your ZeroSSL account.";
            }
            var email = _accountArguments.EmailAddress;
            if (string.IsNullOrWhiteSpace(email) && runLevel.HasFlag(RunLevel.Interactive))
            {
                _input.CreateSpace();
                _input.Show(null, question);
                _input.CreateSpace();
                email = await _input.RequestString(question);
            }
            var newEmails = new List<string>();
            if (allowMultiple)
            {
                newEmails = email.ParseCsv();
                if (newEmails == null)
                {
                    return [];
                }
            }
            else if (!string.IsNullOrWhiteSpace(email))
            {
                newEmails.Add(email);
            }
            newEmails = [.. newEmails.Where(x =>
            {
                try
                {
                    _ = new MailAddress(x);
                    return true;
                }
                catch(Exception ex)
                {
                    _log.Warning(ex, $"Invalid email address specified: {x}");
                    return false;
                }
            })];
            if (newEmails.Count == 0 && !allowNone)
            {
                _log.Warning("No (valid) email address specified");
            }
            return [.. newEmails.Select(x => $"{prefix}{x}")];
        }

        private async Task<EabCredential?> ZeroSslEmailRegistration(RunLevel runLevel)
        {
            var registration = await GetContacts(runLevel, zeroSsl: true);
            var eab = await _zeroSsl.Register(registration.FirstOrDefault() ?? "");
            if (eab?.Success == true)
            {
                return new EabCredential()
                {
                    KeyIdentifier = eab.Kid,
                    Key = eab.Hmac,
                    Algorithm = "HS256"
                };
            }
            _log.Error("Unable to retrieve EAB credentials using the provided email address");
            return null;
        }

        private async Task<EabCredential?> ZeroSslApiKeyRegistration()
        {
            var accessKey = await _input.ReadPassword("API access key");
            var eab = await _zeroSsl.Obtain(accessKey ?? "");
            if (eab?.Success == true)
            {
                return new EabCredential()
                {
                    KeyIdentifier = eab.Kid,
                    Key = eab.Hmac,
                    Algorithm = "HS256"
                };
            }
            _log.Error("Unable to retrieve EAB credentials using the provided API access key");
            return null;
        }

        /// <summary>
        /// Get EAB credentials from command line arguments
        /// </summary>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        public async Task<EabCredential?> FromArguments()
        {
            if (string.IsNullOrWhiteSpace(_accountArguments.EabKeyIdentifier) || string.IsNullOrWhiteSpace(_accountArguments.EabKey)) 
            {
                return null;
            }
            return new EabCredential()
            {
                KeyIdentifier = _accountArguments.EabKeyIdentifier,
                Key = await _secretServiceManager.EvaluateSecret(_accountArguments.EabKey),
                Algorithm = _accountArguments.EabAlgorithm ?? "HS256"
            };
        }

        /// <summary>
        /// Get EAB credentials from the user interactively
        /// </summary>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        private async Task<EabCredential?> FromInteractive()
        {
            var kid = await _input.RequestString("Key identifier");
            var key = await _input.ReadPassword("Key (base64url encoded)");
            if (string.IsNullOrWhiteSpace(kid) || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }
            return new EabCredential()
            {
                KeyIdentifier = kid,
                Key = key,
                Algorithm = _accountArguments.EabAlgorithm ?? "HS256"
            };
        }

        /// <summary>
        /// ZeroSSL registration 
        /// </summary>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        public async Task<EabCredential?> GetZeroSsl(RunLevel runLevel)
        {
            if (runLevel.HasFlag(RunLevel.Unattended))
            {
                if (!string.IsNullOrWhiteSpace(_accountArguments.EmailAddress))
                {
                    return await ZeroSslEmailRegistration(runLevel);
                }
                else
                {
                    _log.Error("ZeroSSL requires an account to use. Either provide EAB credentials using the arguments --eab-key-identifier and --eab-key, or an email address using the argument --emailaddress.");
                    return null;
                }
            }
            var instruction = "ZeroSsl can be used either by setting up a new " +
                            "account using your email address or by connecting it to your existing " +
                            "account using the API access key or pre-generated EAB credentials, which can " +
                            "be obtained from the Developer section of the dashboard.";
            _input.CreateSpace();
            _input.Show(null, instruction);
            _input.CreateSpace();
            var chosen = await _input.ChooseFromMenu(
                        "How would you like to create the account?",
                        [
                           Choice.Create(() => ZeroSslApiKeyRegistration(), "API access key"),
                           Choice.Create(() => ZeroSslEmailRegistration(runLevel), "Email address"),
                           Choice.Create(() => FromInteractive(), "Input EAB credentials")
                        ]);
            return await chosen.Invoke();
        }

        /// <summary>
        /// Get regular EAB credentials, with instructions to the user on how to obtain them from the ACME provider
        /// </summary>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        public async Task<EabCredential?> GetRegular(RunLevel runLevel)
        {
            if (runLevel.HasFlag(RunLevel.Interactive))
            {
                var instruction = 
                    "This ACME endpoint requires an external account. " +
                    "You will need to provide a key identifier and a key to proceed. " +
                    "Please refer to the providers instructions on how to obtain these.";
                _input.CreateSpace();
                _input.Show(null, instruction);
                _input.CreateSpace();
                return await FromInteractive();
            }
            else
            {
                throw new Exception("This server requires EAB credentials. Provide them using the arguments --eab-key-identifier and --eab-key.");
            }
        }
    }
}
