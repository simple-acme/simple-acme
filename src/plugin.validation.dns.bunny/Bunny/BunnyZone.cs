using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{

    class BunnyZoneList
    {
        public List<BunnyZone> Items { get; set; } = new();
    }

    class BunnyZone
    {
        public long Id { get; set; }

        public string Domain { get; set; } = string.Empty;

        public List<BunnyRecords> Records { get; set; } = new();
    }

    class BunnyRecords
    {
        public long Id { get; set; }
        public long Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
