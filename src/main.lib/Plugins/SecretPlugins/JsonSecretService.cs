using PKISharp.WACS.Extensions;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.SecretPlugins
{
    /// <summary>
    /// Save secrets to a JSON file in the configuration folder, protected by ProtectedStrings
    /// </summary>
    /// <remarks>
    /// Initial parsing of the file
    /// </remarks>
    /// <param name="settings"></param>
    /// <param name="log"></param>
    internal class JsonSecretService(ISettings settings, ILogService log, WacsJson wacsJson) : ISecretService
    {
        private FileInfo? _file;
        private CredentialEntryCollection? _secrets;

        /// <summary>
        /// Make references to this provider unique from 
        /// references in other providers
        /// </summary>
        public string Prefix => "json";

        private async Task<CredentialEntryCollection?> Init()
        {
            if (_secrets != null)
            {
                return _secrets;
            }
            var path = settings.Secrets?.Json?.FilePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Join(settings.Client.ConfigurationPath, "secrets.json");
            }
            _file = new FileInfo(path);
            CredentialEntryCollection? parsed;
            try
            {
                if (_file.Exists)
                {
                    var raw = await File.ReadAllTextAsync(_file.FullName);
                    parsed = raw.StartsWith('[') ?
                        new CredentialEntryCollection()
                        {
                            Entries = JsonSerializer.Deserialize(raw, wacsJson.ListCredentialEntry) ?? []
                        } :
                        JsonSerializer.Deserialize(raw, wacsJson.CredentialEntryCollection);
                    if (parsed != null)
                    {
                        log.Debug("Found {x} secrets in {filename}", parsed.Entries?.Count ?? 0, _file.Name);
                        _secrets = parsed;
                    }
                }
                else
                {
                    log.Debug("{filename} not found", _file.Name);
                }
            }
            catch (Exception ex)
            {
                log.Error("Unable to read {filename}: {message}", _file.Name, ex.Message);
            }
            return _secrets;
        }

        /// <summary>
        /// Read secret from the file
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public async Task<string?> GetSecret(string? identifier) => 
            (await Init())?.Entries?.FirstOrDefault(x => string.Equals(x.Key, identifier, StringComparison.OrdinalIgnoreCase))?.Secret?.Value;

        /// <summary>
        /// Add or overwrite secret, return the key to store
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="secret"></param>
        public async Task PutSecret(string identifier, string secret)
        {
            var data = await Init();
            var existing = data?.Entries?.FirstOrDefault(x => x.Key == identifier);
            if (existing != null)
            {
                existing.Secret = new ProtectedString(secret);
            } 
            else
            {
                _secrets ??= new CredentialEntryCollection();
                _secrets.Entries ??= [];
                _secrets.Entries.Add(new CredentialEntry()
                {
                    Key = identifier,
                    Secret = new ProtectedString(secret)
                });
            }
            await Save();
        }

        /// <summary>
        /// Save files back to JSON
        /// </summary>
        private async Task Save()
        {
            if (_file != null && _secrets != null)
            {
                var newData = JsonSerializer.Serialize(_secrets, wacsJson.CredentialEntryCollection);
                if (newData != null)
                {
                    await _file.SafeWrite(newData);
                }
            }
        }

        public async Task<IEnumerable<string>> ListKeys() =>
            (await Init())?.Entries?.
                Select(x => x.Key).
                Where(x => !string.IsNullOrEmpty(x)).
                OfType<string>() ?? [];

        public async Task DeleteSecret(string key)
        {
            var item = _secrets?.Entries?.Where(x => x.Key == key).FirstOrDefault();
            if (item != null)
            {
                _ = _secrets?.Entries?.Remove(item);
                await Save();
            }
        }

        public async Task Encrypt()
        {
            await Init();
            await Save();
        }
    }

    /// <summary>
    /// Internal data storage format
    /// </summary>
    internal class CredentialEntryCollection
    {
        [JsonPropertyName("$schema")]
        public string Schema { get; set; } = "https://simple-acme.com/schema/secrets.json";
        public List<CredentialEntry>? Entries { get; set; }
    }

    /// <summary>
    /// Interal data storage format
    /// </summary>
    internal class CredentialEntry
    {
        public string? Key { get; set; }
        public ProtectedString? Secret { get; set; }
    }
}
