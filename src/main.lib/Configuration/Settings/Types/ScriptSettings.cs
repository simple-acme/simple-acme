using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types
{
    /// <summary>
    /// Options for installation and DNS scripts
    /// </summary>
    public interface IScriptSettings
    {
        string? PowershellExecutablePath { get; }
        int Timeout { get; }
    }

    internal class InheritScriptSettings(params IEnumerable<ScriptSettings?> chain) : InheritSettings<ScriptSettings>(chain), IScriptSettings
    {
        public string? PowershellExecutablePath => Get(x => x.PowershellExecutablePath);
        public int Timeout => Get(x => x.Timeout) ?? 600;
    }

    public class ScriptSettings
    {
        [SettingsValue(
            Default = "600",
            Description = "Time in seconds to allow installation, executing and validation scripts to run before terminating them forcefully.")]
        public int? Timeout { get; set; }

        [SettingsValue(
            Default = "powershell.exe",
            SubType = "path",
            Description = "Customize this value to use a different version of Powershell to execute <code>.ps1</code> scripts. E.g. <code>C:\\\\Program Files\\\\PowerShell\\\\6.0.0\\\\pwsh.exe</code> for Powershell Core 6.")]
        public string? PowershellExecutablePath { get; set; }
    }
}