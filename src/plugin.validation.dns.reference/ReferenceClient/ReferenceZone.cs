namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    /// <summary>
    /// Represents a DNS zone found at the provider
    /// </summary>
    class ReferenceZone
    {
        /// <summary>
        /// Name of the zone, e.g. "example.com."
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }
}
