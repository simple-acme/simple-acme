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

    public class ScriptSecretsSettings
    {
        [SettingsValue(
            SubType = "path",
            Description = "Location of the secret getting script.")]
        public string? Get { get; set; }

        [SettingsValue(
            NullBehaviour = "defaults to <code>-key {key}</code>",
            Description = "Arguments to pass to the script that retreives a secret. Supported variable substitutions are " +
            "<div class=\"callout-block callout-block-success mt-3\">" +
            "<div class=\"content\">" +
            "<table class=\"table table-bordered\">" +
            "<tr><th class=\"col-md-3\">Value</th><th>Meaning</th></tr>" +
            "<tr><td>{key}</td><td>The identifier of the secret being requested, e.g. the \"mytoken\" part of <pre>vault://script/mytoken</pre></td></tr>" +
            "<tr><td>{operation}</td><td>Currently hard coded to \"get\", may get \"delete\" and \"set\" at some point in the future.</td></tr>" +
            "<tr><td>{vault://vault/key}</td><td>Pass a secret from one of the other vaults to the script. Note that you cannot self-reference.</td></tr>" +
            "</table></div></div>")]
        public string? GetArguments { get; set; }
    }
}