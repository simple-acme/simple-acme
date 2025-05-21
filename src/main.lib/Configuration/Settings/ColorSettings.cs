namespace PKISharp.WACS.Configuration.Settings
{
    /// <summary>
    /// Colors
    /// </summary>
    public interface IColorSettings
    {
        string? Background { get; }
    }

    internal class ColorSettings : IColorSettings
    {
        public string? Background { get; set; }
    }
}