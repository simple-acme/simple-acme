namespace PKISharp.WACS.Configuration.Settings
{
    /// <summary>
    /// Settings for script secret store
    /// </summary>
    public interface IScriptSecretsSettings
    {
        string? Get { get; }
        string? GetArguments { get; }
    }

    internal class ScriptSecretsSettings : IScriptSecretsSettings
    {
        public string? Get { get; set; }
        public string? GetArguments { get; set; }
    }
}