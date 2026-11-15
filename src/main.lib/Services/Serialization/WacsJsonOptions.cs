using System.Text.Json;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Services.Serialization
{
    /// <summary>
    /// Write options writes modern style
    /// </summary>
    internal class WacsJsonOptionsFactory
    {
        public WacsJsonOptionsFactory(
            ProtectedStringFactory protectedStringFactory,
            PluginOptionsConverter pluginConverter,
            PluginOptionsListConverter pluginOptionsListConverter,
            ISettings settings)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            options.Converters.Add(new ProtectedStringConverter(settings, protectedStringFactory));
            options.Converters.Add(pluginOptionsListConverter);
            options.Converters.Add(pluginConverter);
            Options = options;
        }
        public JsonSerializerOptions Options { get; }
    }
}
