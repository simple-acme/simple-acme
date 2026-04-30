using ACMESharp;
using ACMESharp.Protocol;
using ACMESharp.Protocol.Resources;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PKISharp.WACS.Clients.Acme
{
    [JsonSerializable(typeof(AccountSigner))]
    [JsonSerializable(typeof(AccountDetails))]
    [JsonSerializable(typeof(ServiceDirectory))]
    [JsonSerializable(typeof(AcmeOrderDetails))]
    internal partial class AcmeClientJson : JsonSerializerContext
    {
        public static AcmeClientJson Insensitive { get; } = new AcmeClientJson(new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });
    }

    /// <summary>
    /// Main class that talks to the ACME server
    /// </summary>
    internal class AcmeClientManager(
        IInputService inputService,
        ArgumentsParser arguments,
        ILogService log,
        IAcmeLogger acmeLogger,
        ISettings settings,
        AccountManager accountManager,
        IProxyService proxy,
        AcmeCredentialReader acmeCredentials)
    {

        private AcmeProtocolClient? _anonymousClient;
        private readonly Dictionary<string, AcmeClient> _authorizedClients = [];

        /// <summary>
        /// Load the directory and create AcmeProtocolClient that we will use
        /// to setup new accounts using anonymous context (or EAB)
        /// </summary>
        /// <returns></returns>
        private async Task<AcmeProtocolClient> CreateAnonymousClient()
        {
            var httpClient = await proxy.GetHttpClient();
            httpClient.BaseAddress = settings.BaseUri;
            log.Verbose("Constructing ACME protocol client...");
            var client = new AcmeProtocolClient(httpClient, acmeLogger, usePostAsGet: settings.Acme.PostAsGet);
            client.Directory = await EnsureServiceDirectory(client);
            return client;
        }

        /// <summary>
        /// Get server metadata
        /// </summary>
        /// <returns></returns>
        internal async Task<DirectoryMeta?> GetMetaData()
        {
            // Create anonymous client if we need it
            _anonymousClient ??= await CreateAnonymousClient();
            return _anonymousClient.Directory.Meta;
        }

        /// <summary>
        /// Load the real client that will be used for validation etc.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        internal async Task<AcmeClient> CreateAuthorizedClient(RunLevel runLevel, string? name = null)
        {
            // Create anonymous client if we need it
            _anonymousClient ??= await CreateAnonymousClient();

            // Try to load prexisting account
            var account = accountManager.LoadAccount(name);
            if (account != null)
            {
                log.Verbose("Using existing ACME account");
            }
            else
            {
                log.Verbose("No account found, creating new one");
                account = await SetupAccount(_anonymousClient, runLevel);
                if (account == null)
                {
                    throw new Exception("AcmeClient was unable to find or create an account");
                }
                // Save newly created account to disk
                await accountManager.StoreAccount(account, name);
            }

            // Create authorized account
            var httpClient = await proxy.GetHttpClient(settings.Acme.ValidateServerCertificate);
            var ret = new AcmeClient(httpClient, log, acmeLogger, settings, _anonymousClient.Directory, account);
            if (!string.IsNullOrWhiteSpace(name))
            {
                log.Debug("Using named account {name}...", name);
            }
            else
            {
                log.Debug("Using default account...", name);
            }
            return ret;
        }

        /// <summary>
        /// Guess where we might find the service directory, based on the base URI 
        /// </summary>
        /// <returns></returns>
        internal List<string> GetDirectorUrls()
        {
            var urlsToTry = new List<string>([""]);
            if (settings.BaseUri.Host.EndsWith(".letsencrypt.org") &&
                string.IsNullOrEmpty(settings.BaseUri.PathAndQuery.TrimEnd('/')))
            {
                // For Let's Encrypt, try the /directory endpoint first,
                // because historically we have only configured the host
                // name (e.g. https://acme-v02.api.letsencrypt.org/)
                urlsToTry.Insert(0, "directory");
            }
            else
            {
                // For other ACME providers, try the user specified endpoint
                // first,but offer fallback to /directory for backwards
                // compatiblity with how the client used to behave.
                urlsToTry.Insert(1, "directory");
            }
            return urlsToTry;
        }

        /// <summary>
        /// Make sure that we find a service directory
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task<ServiceDirectory> EnsureServiceDirectory(AcmeProtocolClient client)
        {
            ServiceDirectory? directory;
            foreach (var urlToTry in GetDirectorUrls())
            {
                try
                {
                    log.Debug("Getting service directory from {url}...", string.IsNullOrWhiteSpace(urlToTry) ? settings.BaseUri : "/" + urlToTry);
                    directory = await client.Backoff(async () => await client.GetDirectoryAsync(urlToTry), log);
                    if (directory != null)
                    {
                        return directory;
                    }
                }
                catch
                {
                }
            }
            throw new Exception("Unable to get service directory");
        }

        /// <summary>
        /// Get AcmeClient using the default account
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        internal async Task<AcmeClient> GetClient(RunLevel runLevel, string? name = null)
        {
            var key = name ?? "";
            if (_authorizedClients.TryGetValue(key, out var value))
            {
                return value;
            }
            var ret = await CreateAuthorizedClient(runLevel, name) ?? throw new InvalidOperationException("Failed to initialize Acme client");
            _authorizedClients.Add(key, ret);
            return ret;
        }

        /// <summary>
        /// Setup a new ACME account
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task<Account?> SetupAccount(AcmeProtocolClient client, RunLevel runLevel)
        {
            // Accept the terms of service, if defined by the server
            try
            {
                var (_, filename, content) = await client.GetTermsOfServiceAsync();
                log.Verbose("Terms of service downloaded");
                if (!string.IsNullOrEmpty(filename) && content != null)
                {
                    if (!await AcceptTos(filename, content))
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error getting terms of service");
            }

            var eabRequired = client.Directory?.Meta?.ExternalAccountRequired ?? false;
            var eabArgs = await acmeCredentials.FromArguments();

            // Warn about unneeded EAB
            if (!eabRequired && eabArgs != null)
            {
                inputService.CreateSpace();
                inputService.Show(null, "You have provided an external account binding key, even though " +
                    "the server does not indicate that this is required. We will attempt to register " +
                    "using this key anyway.");
                inputService.CreateSpace();
            }

            // Get EAB if required by the server
            if (eabRequired && eabArgs == null)
            {
                eabArgs = settings.BaseUri.Host.Contains("zerossl.com")
                    ? await acmeCredentials.GetZeroSsl(runLevel)
                    : await acmeCredentials.GetRegular(runLevel);
                if (eabArgs == null)
                {
                    throw new Exception("Unable to retrieve required credentials");
                }
            }

            // Get contacts if EAB is not required
            var contacts = eabRequired
                ? [] 
                : await acmeCredentials.GetContacts(runLevel);

            var newAccount = accountManager.NewAccount();
            AccountDetails newAccountDetails;
            try
            {
                newAccountDetails = await CreateAccount(client, newAccount.Signer, contacts, eabArgs);
            }
            catch (AcmeProtocolException apex)
            {
                // Some non-ACME compliant server may not support ES256 or other
                // algorithms, so attempt fallback to RS256
                if (apex.ProblemType == ProblemType.BadSignatureAlgorithm && newAccount.Signer.KeyType != "RS256")
                {
                    newAccount = accountManager.NewAccount("RS256");
                    newAccountDetails = await CreateAccount(client, newAccount.Signer, contacts, eabArgs);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "Error creating account");
                return null;
            }
            if (newAccountDetails == default)
            {
                return null;
            }
            return new Account(newAccountDetails, newAccount.Signer);
        }

        /// <summary>
        /// Attempt to create an account using specific parameters
        /// </summary>
        /// <param name="client"></param>
        /// <param name="signer"></param>
        /// <param name="contacts"></param>
        /// <param name="eabAlg"></param>
        /// <param name="eabKid"></param>
        /// <param name="eabKey"></param>
        /// <returns></returns>
        private async Task<AccountDetails> CreateAccount(
            AcmeProtocolClient client, AccountSigner signer,
            string[]? contacts, EabCredential? eabCredential)
        {
            if (client.Account != null)
            {
                throw new Exception("Client already has an account!");
            }
            ExternalAccountBinding? externalAccount = null;
            if (eabCredential != null)
            {
                externalAccount = new ExternalAccountBinding(
                    eabCredential.Algorithm,
                    signer.JwsTool().ExportEab(),
                    eabCredential.KeyIdentifier,
                    eabCredential.Key,
                    client.Directory?.NewAccount ?? "");
            }
            await client.ChangeAccountKeyAsync(signer.JwsTool());
            return await client.Retry(
                () => client.CreateAccountAsync(
                    contacts,
                    termsOfServiceAgreed: true,
                    externalAccountBinding: externalAccount?.Payload() ?? null), log);
        }

        /// <summary>
        /// Ask the user to accept the terms of service dictated 
        /// by the ACME service operator
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        private async Task<bool> AcceptTos(string filename, byte[] content)
        {
            var tosPath = Path.Combine(settings.Client.ConfigurationPath, filename);
            log.Verbose("Writing terms of service to {path}", tosPath);
            await File.WriteAllBytesAsync(tosPath, content);
            inputService.CreateSpace();
            inputService.Show($"Terms of service", tosPath);
            inputService.CreateSpace();
            if (arguments.GetArguments<AccountArguments>()?.AcceptTos ?? false)
            {
                return true;
            }
            if (await inputService.PromptYesNo($"Open in default application?", false))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = tosPath,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Unable to start application");
                }
            }
            return await inputService.PromptYesNo($"Do you agree with the terms?", true);
        }

        /// <summary>
        /// Update email address for the current account
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        internal async Task ChangeContacts(RunLevel runLevel, string? name = null)
        {
            var client = await GetClient(runLevel, name);
            var contacts = await acmeCredentials.GetContacts(runLevel);
            var newDetails = await client.UpdateAccountAsync(contacts);
            if (newDetails.Payload != null)
            {
                client.Account.Details = newDetails;
                await accountManager.StoreAccount(client.Account, name);
            }
        }
    }
}