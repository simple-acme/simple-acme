namespace PKISharp.WACS.Configuration.Settings
{
    public interface IInstallationSettings
    {
        /// <summary>
        /// Default plugin(s) to select 
        /// </summary>
        string? DefaultInstallation { get; }
    }

    internal class InstallationSettings : IInstallationSettings
    {
        public string? DefaultInstallation { get; set; }
    }
}