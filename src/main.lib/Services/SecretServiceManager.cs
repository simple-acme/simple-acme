using Autofac;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    public class SecretServiceManager(
        ILifetimeScope scope,
        IInputService input,
        IPluginService pluginService,
        ILogService log)
    {
        private readonly List<ISecretProvider> _providers = [.. pluginService.
                GetSecretServices().
                Select(b => scope.Resolve(b.Backend)).
                OfType<ISecretProvider>()];

        private readonly List<ISecretService> _services = [.. pluginService.
                GetSecretServices().
                Select(b => scope.Resolve(b.Backend)).
                OfType<ISecretService>()];

        private record DecomposedKey
        {
            public string? Provider;
            public string? Key;
            public string? Original;
            public bool Valid;

            public DecomposedKey(string? full)
            {
                Original = full;
                if (string.IsNullOrWhiteSpace(full))
                {
                    return;
                }
                if (!full.StartsWith(VaultPrefix))
                {
                    return;
                }
                var remainingValue = full[VaultPrefix.Length..];
                var parts = remainingValue.Split('/');
                if (parts.Length != 2)
                {
                    return;
                }
                Provider = parts[0];
                Key = parts[1];
                Valid = true;
            }
        }

        /// <summary>
        /// Global const to trigger vault evaluation vs. 
        /// interpreting secrets as literal strings
        /// </summary>
        public const string VaultPrefix = "vault://";

        /// <summary>
        /// Get a secret from interactive mode setup
        /// </summary>
        /// <param name="purpose"></param>
        /// <returns></returns>
        public async Task<string?> GetSecret(string purpose, string? @default = null, string? none = null, bool required = false, bool multiline = false)
        {
            var stop = false;
            string? ret = null;
            // While loop allows the "Find in vault" option
            // to be cancelled so that the user can still
            // input a new password if it's not found yet
            // without having to restart the process.
            while (!stop && string.IsNullOrEmpty(ret))
            {
                var options = new List<Choice<Func<Task<string?>>>>();
                if (!required)
                {
                    options.Add(Choice.Create<Func<Task<string?>>>(
                        () => {
                            stop = true;
                            return Task.FromResult(none);
                        },
                        description: "None"));
                }
                options.Add(Choice.Create<Func<Task<string?>>>(
                    async () => {
                        stop = true;
                        if (multiline)
                        {
                            return await input.RequestString(purpose, true);
                        }
                        else
                        {
                            return await input.ReadPassword(purpose);
                        }
                    },
                    description: "Type/paste in console"));
                options.Add(Choice.Create(
                        () => FindSecret(),
                        description: "Search in vault"));
                if (@default != null)
                {
                    var description = "Default from settings.json";
                    if (string.IsNullOrWhiteSpace(@default))
                    {
                        description += " (currently empty!)";
                    }
                    options.Add(Choice.Create(
                        () => { 
                            stop = true;
                            return Task.FromResult<string?>(@default); 
                        },
                        @default: true,
                        description: description));
                }

                // Handle undefined input as direct password
                Choice<Func<Task<string?>>> processUnkown(string? unknown) => Choice.Create<Func<Task<string?>>>(() => Task.FromResult(unknown));

                var chosen = await input.ChooseFromMenu("Choose from the menu", options, (x) => processUnkown(x));
                ret = await chosen.Invoke();
            }

            if (ret == none)
            {
                return none;
            }
            if (ret == @default || ret == null)
            {
                return @default;
            }

            // Offer to save in list
            if (!ret.StartsWith(VaultPrefix))
            {
                var save = await input.PromptYesNo($"Save to vault for future reuse?", false);
                if (save)
                {
                    return await ChooseKeyAndStoreSecret(ret);
                }
            }
            return ret;
        }

        /// <summary>
        /// Add a secret to the backend from the main menu
        /// </summary>
        /// <returns></returns>
        public async Task Encrypt() {
            foreach (var backend in _services) {
                await backend.Encrypt();
            }
        }

        /// <summary>
        /// Add a secret to the backend from the main menu
        /// </summary>
        /// <returns></returns>
        public async Task<string?> AddSecret()
        {
            var secret = await input.ReadPassword("Secret");
            if (!string.IsNullOrWhiteSpace(secret))
            {
                return await ChooseKeyAndStoreSecret(secret);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Pick key and backend the secret in the vault
        /// </summary>
        /// <param name="secret"></param>
        /// <returns></returns>
        private async Task<ISecretService> ChooseBackend()
        {
            if (_services.Count == 1)
            {
                return _services[0];
            }
            return await input. 
                ChooseRequired("Choose secret store",
                _services, x => Choice.Create(x, description: x.GetType().ToString()));
        }

        /// <summary>
        /// Pick key and backend the secret in the vault
        /// </summary>
        /// <param name="secret"></param>
        /// <returns></returns>
        private async Task<string> ChooseKeyAndStoreSecret(string secret)
        {
            var backend = await ChooseBackend();
            var key = "";
            while (string.IsNullOrEmpty(key))
            {
                key = await input.RequestString("Please provide a unique name to reference this secret", false);
                key = key.Trim().ToLower().Replace(" ", "-");
                var keys = await backend.ListKeys();
                if (keys.Contains(key))
                {
                    var overwrite = await input.PromptYesNo($"Key {key} already exists in vault, overwrite?", true);
                    if (!overwrite)
                    {
                        key = null;
                    }
                }
            }
            await backend.PutSecret(key, secret);
            return FormatKey(backend, key);
        }

        /// <summary>
        /// Format the key
        /// </summary>
        /// <returns></returns>
        public static string FormatKey(ISecretService store, string key) => $"{VaultPrefix}{store.Prefix}/{key}";

        /// <summary>
        /// List secrets currently in vault as choices to pick from
        /// </summary>
        /// <returns></returns>
        private async Task<string?> FindSecret()
        {
            var backend = await ChooseBackend();
            var keys = await backend.ListKeys();
            var chosenKey = await input.ChooseOptional(
                "Which vault secret do you want to use?",
                keys,
                (key) => Choice.Create<string?>(key, description: FormatKey(backend, key)),
                "Cancel");
            if (chosenKey == null)
            {
                return null;
            }
            else
            {
                return FormatKey(backend, chosenKey);
            }
        }

        /// <summary>
        /// Shortcut method
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<string?> EvaluateSecret(ProtectedString? input) => await EvaluateSecret(input?.Value);

        /// <summary>
        /// Try to interpret the secret input as a vault reference
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<string?> EvaluateSecret(string? input)
        {
            var key = new DecomposedKey(input);
            if (!key.Valid)
            {
                return key.Original;
            }
            var provider = GetProvider(key.Provider);
            if (provider != null)
            {
                var ret = await provider.GetSecret(key.Key);
                return ret;
            }
            return input;
        }

        /// <summary>
        /// Manage secrets from the main menu
        /// </summary>
        /// <returns></returns>
        internal async Task ManageSecrets()
        {
            var exit = false;
            while (!exit)
            {
                var choices = new List<Choice<Func<Task>>>();
                foreach (var backend in _services)
                {
                    var keys = await backend.ListKeys();
                    choices.AddRange(keys.Select(key => Choice.Create(
                        () => EditSecret(backend, key),
                        description: FormatKey(backend, key))));
                }   

                choices.Add(Choice.Create<Func<Task>>(AddSecret, "Add secret", command: "A"));
                choices.Add(Choice.Create(() => { 
                    exit = true; 
                    return Task.CompletedTask; 
                }, "Back to main menu", command: "Q", @default: true));
                var chosen = await input.ChooseFromMenu("Choose an existing secret to manage, add a new one", choices);
                await chosen.Invoke();
            }

        }

        /// <summary>
        /// Edit or delete existing secret
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        internal async Task EditSecret(ISecretService backend, string key) {
            var exit = false;
            while (!exit)
            {
                var secret = await backend.GetSecret(key);
                input.CreateSpace();
                input.Show("Reference", key);
                input.Show("Secret", "********");
                var choices = new List<Choice<Func<Task>>>
                {
                    Choice.Create(() => ShowSecret(backend, key), "Show secret", command: "S"),
                    Choice.Create(() => UpdateSecret(backend, key), "Update secret", command: "U"),
                    Choice.Create(() =>
                    {
                        exit = true;
                        return DeleteSecret(backend, key);
                    }, "Delete secret", command: "D"),
                    Choice.Create(() =>
                    {
                        exit = true;
                        return Task.CompletedTask;
                    }, "Back to list", command: "Q", @default: true)
                };
                var chosen = await input.ChooseFromMenu("Choose an option", choices);
                await chosen.Invoke();
            }
        }

        /// <summary>
        /// Delete a secret from the backend
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private Task DeleteSecret(ISecretService backend, string key)
        {
            backend.DeleteSecret(key);
            log.Warning($"Secret {key} deleted from {backend.Prefix} store");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Update a secret in the backend
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private async Task UpdateSecret(ISecretService backend, string key)
        {
            var secret = await input.ReadPassword("Secret");
            if (!string.IsNullOrWhiteSpace(secret))
            {
                await backend.PutSecret(key, secret);
            }
            else
            {
                log.Warning("No input provided, update cancelled");
            }
        }

        /// <summary>
        /// Show secret on screen
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private async Task ShowSecret(ISecretService backend, string key) {
            var secret = await backend.GetSecret(key);
            input.Show("Secret", secret);
        }

        /// <summary>
        /// Store a secret directly into a vault
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task StoreSecret(string? uri, string? value)
        {
            if (uri == null)
            {
                log.Error("Argument --{VaultKey} not specified", nameof(MainArguments.VaultKey).ToLower());
                return;
            }
            if (value == null)
            {
                log.Error("Argument --{VaultSecret} not specified", nameof(MainArguments.VaultSecret).ToLower());
                return;
            }
            if (!uri.StartsWith(VaultPrefix))
            {
                log.Error("Argument --{VaultKey} should start with {VaultPrefix}", nameof(MainArguments.VaultKey).ToLower(), VaultPrefix);
                return;
            }
            var key = new DecomposedKey(uri);
            if (!key.Valid || key.Key == null)
            {
                log.Error("Argument --{VaultKey} is invalid", nameof(MainArguments.VaultKey).ToLower(), VaultPrefix);
                return;
            }
            var backend = GetService(key.Provider);
            if (backend == null)
            {
                log.Error("Vault backend {backendKey} is not known. Default is json, other values require plugins", key.Provider);
                return;
            }
            await backend.PutSecret(key.Key, value);
            log.Information("Vault secret {key} successfully stored in backend {backend}", key, key.Provider);
        }

        private ISecretService? GetService(string? key) => _services.FirstOrDefault(b => string.Equals(b.Prefix, key));
        private ISecretProvider? GetProvider(string? key) => _providers.FirstOrDefault(b => string.Equals(b.Prefix, key));
    } 
}