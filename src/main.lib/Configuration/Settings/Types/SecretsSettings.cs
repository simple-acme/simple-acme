using PKISharp.WACS.Configuration.Settings.Types.Secrets;
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Configuration.Settings.Types
{
    /// <summary>
    /// Settings for secret management
    /// </summary>
    public interface ISecretsSettings
    {
        IJsonSettings? Json { get;}
        IScriptSecretsSettings? Script { get; }
    }

    internal class InheritSecretsSettings(params IEnumerable<SecretsSettings?> chain) : InheritSettings<SecretsSettings>(chain), ISecretsSettings
    {
        public IJsonSettings Json => new InheritJsonSettings(Chain.Select(c => c?.Json));
        public IScriptSecretsSettings Script => new InheritScriptSecretsSettings(Chain.Select(c => c?.Script));
    }

    internal class SecretsSettings
    {
        public JsonSettings? Json { get; set; }
        public ScriptSecretsSettings? Script { get; set; }
    }
}