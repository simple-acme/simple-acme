namespace PKISharp.WACS.Configuration.Settings
{
    /// <summary>
    /// Settings for secret management
    /// </summary>
    public class SecretsSettings
    {
        public JsonSettings? Json { get; set; }
        public ScriptSecretsSettings? Script { get; set; }
    }
}