using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Services
{
    internal class GlobalValidationPluginOptionsCollection
    {
        [JsonPropertyName("$schema")]
        public string Schema { get; set; } = "https://simple-acme.com/schema/validation.json";
        public List<GlobalValidationPluginOptions>? Options { get; set; }
    }
}
