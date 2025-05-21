namespace PKISharp.WACS.Configuration.Settings
{
    public interface IClientSettings
    {
        string ClientName { get; }
        string ConfigurationPath { get; }
        string? LogPath { get; }
        bool VersionCheck { get; }
    }

    internal class ClientSettings : IClientSettings
    {
        public string ClientName { get; set; } = "simple-acme";
        public string ConfigurationPath { get; set; } = "";
        public string? LogPath { get; set; }
        public bool VersionCheck { get; set; }
    }
}