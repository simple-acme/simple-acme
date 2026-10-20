using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    /// <summary>
    /// Wrapper for the response of GET domains?filter={name}
    /// </summary>
    class Level27DomainList
    {
        [JsonPropertyName("domains")]
        public List<Level27Domain> Domains { get; set; } = [];
    }

    /// <summary>
    /// A single domain (zone) as returned by the Level27 API
    /// </summary>
    class Level27Domain
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("fullname")]
        public string Fullname { get; set; } = string.Empty;
    }

    /// <summary>
    /// Internal representation of a Level27 DNS zone that is used
    /// throughout the plugin.
    /// </summary>
    class Level27Zone
    {
        public long Id { get; set; }

        /// <summary>
        /// Fully qualified name of the zone, e.g. "example.com"
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Wrapper for the response of GET domains/{id}/records
    /// </summary>
    class Level27RecordList
    {
        [JsonPropertyName("records")]
        public List<Level27Record> Records { get; set; } = [];
    }

    /// <summary>
    /// Wrapper for the response of POST domains/{id}/records
    /// </summary>
    class Level27RecordResponse
    {
        [JsonPropertyName("record")]
        public Level27Record? Record { get; set; }
    }

    /// <summary>
    /// A single DNS record as returned by the Level27 API
    /// </summary>
    class Level27Record
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
