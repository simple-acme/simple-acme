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
    internal class JsonSecretService : ISecretService
    {
        private readonly FileInfo _file;
        private readonly List<CredentialEntry> _secrets;
        private readonly ILogService _log;
        private readonly ISettingsService _settings;
        private readonly WacsJson _wacsJson;

        /// <summary>
        /// Make references to this provider unique from 
        /// references in other providers
        /// </summary>
        public string Prefix => "json";

        /// <summary>
        /// Initial parsing of the file
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="log"></param>
        public JsonSecretService(ISettingsService settings, ILogService log, WacsJson wacsJson)
        {
            _log = log;
            _wacsJson = wacsJson;
            _settings = settings;
            var path = _settings.Secrets?.Json?.FilePath;
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Join(settings.Client.ConfigurationPath, "secrets.json");
            }
            _file = new FileInfo(path);
            _secrets = [];
            if (_file.Exists)
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                options.Converters.Add(new ProtectedStringConverter(_log, _settings));
                var parsed = JsonSerializer.Deserialize(File.ReadAllText(_file.FullName), _wacsJson.ListCredentialEntry);
                if (parsed == null)
                {
                    _log.Error("Unable to parse {filename}", _file.Name);
                }
                else
                {
                    _secrets = parsed;
                    _log.Debug("Found {x} secrets in {filename}", parsed.Count, _file.Name);
                }
            }
            else
            {
                _log.Debug("{filename} not found", _file.Name);
            }
        }

        /// <summary>
        /// Read secret from the file
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public Task<string?> GetSecret(string? identifier) => 
            Task.FromResult(_secrets.FirstOrDefault(x => string.Equals(x.Key, identifier, StringComparison.OrdinalIgnoreCase))?.Secret?.Value);

        /// <summary>
        /// Add or overwrite secret, return the key to store
        /// </summary>
        /// <param name="identifier"></param>
        /// <param name="secret"></param>
        public async Task PutSecret(string identifier, string secret)
        {
            var existing = _secrets.FirstOrDefault(x => x.Key == identifier);
            if (existing != null)
            {
                existing.Secret = new ProtectedString(secret);
            } 
            else
            {
                _secrets.Add(new CredentialEntry()
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
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ProtectedStringConverter(_log, _settings));
            options.WriteIndented = true;
            options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            var newData = JsonSerializer.Serialize(_secrets, _wacsJson.ListCredentialEntry);
            if (newData != null)
            {
                await _file.SafeWrite(newData);
            }
        }

        public IEnumerable<string> ListKeys() => 
            _secrets.
                Select(x => x.Key).
                Where(x => !string.IsNullOrEmpty(x)).
                OfType<string>();

        public async Task DeleteSecret(string key)
        {
            var item = _secrets.Where(x => x.Key == key).FirstOrDefault();
            if (item != null)
            {
                _ = _secrets.Remove(item);
                await Save();
            }
        }

        public async Task Encrypt() => await Save();

        /// <summary>
        /// Interal data storage format
        /// </summary>
        internal class CredentialEntry
        {
            public string? Key { get; set; }
            public ProtectedString? Secret { get; set; }
        }
    }
}
