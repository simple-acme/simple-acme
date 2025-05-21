namespace PKISharp.WACS.Configuration.Settings
{
    public class ClientSettings
    {
        public string ClientName { get; set; } = "simple-acme";
        public string ConfigurationPath { get; set; } = "";
        public string? LogPath { get; set; }
        public bool VersionCheck { get; set; }
    }
}