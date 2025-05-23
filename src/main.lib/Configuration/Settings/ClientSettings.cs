using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings
{
    public interface IClientSettings
    {
        string ClientName { get; }
        string ConfigurationPath { get; }
        string? LogPath { get; }
        bool? VersionCheck { get; }
    }

    internal class InheritClientSettings(params IEnumerable<ClientSettings> chain) : InheritSettings<ClientSettings>(chain), IClientSettings
    {
        public string ClientName => Get(x => x.ClientName) ?? "simple-acme";
        public string ConfigurationPath => Get(x => x.ConfigurationPath) ?? "";
        public string? LogPath => Get(x => x.LogPath);
        public bool? VersionCheck => Get(x => x.VersionCheck) ?? false;
    }

    internal class ClientSettings
    {
        public string? ClientName { get; set; }
        public string? ConfigurationPath { get; set; }
        public string? LogPath { get; set; }
        public bool? VersionCheck { get; set; }
    }
}