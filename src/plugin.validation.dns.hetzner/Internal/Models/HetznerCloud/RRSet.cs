using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns.Internal.Models.HetznerCloud;

internal sealed class RRSet
{
    public RRSet(HetznerRecord record)
    {
        this.Name = record.name;
        this.Type = record.type;
        this.Ttl = record.ttl;
        this.Records = new[]
        {
            new RecordValue($"\"{record.value}\"", "win-acme validation record")
        };
    }

    [JsonPropertyName("name")]
    public string Name { get; }

    [JsonPropertyName("type")]
    public string Type { get; }

    [JsonPropertyName("ttl")]
    public int Ttl { get; }

    [JsonPropertyName("records")]
    public RecordValue[] Records { get; set; }

    internal record RecordValue(string value, string comment);
}