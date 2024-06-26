using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Services.Serialization
{
    /// <summary>
    /// forces a re-calculation of the protected data according to current machine setting in EncryptConfig when
    /// writing the json for renewals and options for plugins
    /// </summary>
    public class ProtectedStringConverter(ILogService log, ISettingsService settings) : JsonConverter<ProtectedString>
    {
        public override ProtectedString? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => 
            new(reader.GetString() ?? "", log);

        public override void Write(Utf8JsonWriter writer, ProtectedString value, JsonSerializerOptions options) => 
            writer.WriteStringValue(value?.DiskValue(settings.Security.EncryptConfig));
    }
}