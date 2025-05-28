using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types.Secrets
{
    /// <summary>
    /// Settings for script secret store
    /// </summary>
    public interface IScriptSecretsSettings
    {
        string? Get { get; }
        string? GetArguments { get; }
    }

    internal class InheritScriptSecretsSettings(IEnumerable<ScriptSecretsSettings?> chain) : InheritSettings<ScriptSecretsSettings>(chain), IScriptSecretsSettings
    {
        public string? Get => Get(x => x.Get);
        public string? GetArguments => Get(x => x.GetArguments);
    }


    internal class ScriptSecretsSettings
    {
        public string? Get { get; set; }
        public string? GetArguments { get; set; }
    }
}