using System.Collections.Generic;

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

    internal class InheritScriptSettings(params IEnumerable<ScriptSettings?> chain) : InheritSettings<ScriptSettings>(chain), IScriptSettings
    {
        public string? PowershellExecutablePath => Get(x => x.PowershellExecutablePath);
        public int Timeout => Get(x => x.Timeout) ?? 600;
    }

    internal class ScriptSettings
    {
        public int? Timeout { get; set; }
        public string? PowershellExecutablePath { get; set; }
    }
}