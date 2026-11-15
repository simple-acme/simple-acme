namespace PKISharp.WACS.Configuration.Settings.Types
{
    public interface IClientSettings
    {
        string ConfigRoot { get; }
        string ClientName { get; }
        string ConfigurationPath { get; }
        string PluginPath { get; }
        string LogPath { get; }
        bool? VersionCheck { get; }
    }
}
