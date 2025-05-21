namespace PKISharp.WACS.Configuration.Settings
{
    /// <summary>
    /// Options for installation and DNS scripts
    /// </summary>
    public interface IScriptSettings
    {
        string? PowershellExecutablePath { get; }
        int Timeout { get; }
    }

    internal class ScriptSettings : IScriptSettings
    {
        public int Timeout { get; set; } = 600;
        public string? PowershellExecutablePath { get; set; }
    }
}