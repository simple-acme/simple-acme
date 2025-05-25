namespace PKISharp.WACS.Configuration.Settings.Secrets
{
    /// <summary>
    /// Settings for json secret store
    /// </summary>
    public interface IJsonSettings
    {
        string? FilePath { get; }
    }

    internal class JsonSettings : IJsonSettings
    {
        public string? FilePath { get; set; }
    }
}