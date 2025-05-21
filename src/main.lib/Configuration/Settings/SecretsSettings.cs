namespace PKISharp.WACS.Configuration.Settings
{
    /// <summary>
    /// Settings for secret management
    /// </summary>
    public interface ISecretsSettings
    {
        IJsonSettings? Json { get;}
        IScriptSecretsSettings? Script { get; }
    }

    internal class SecretsSettings : ISecretsSettings
    {
        public JsonSettings? Json { get; set; }
        public ScriptSecretsSettings? Script { get; set; }
        IJsonSettings? ISecretsSettings.Json => Json;
        IScriptSecretsSettings? ISecretsSettings.Script => Script;
    }
}