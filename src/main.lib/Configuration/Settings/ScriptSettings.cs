namespace PKISharp.WACS.Configuration.Settings
{
    /// <summary>
    /// Options for installation and DNS scripts
    /// </summary>
    public class ScriptSettings
    {
        public int Timeout { get; set; } = 600;
        public string? PowershellExecutablePath { get; set; }
    }
}