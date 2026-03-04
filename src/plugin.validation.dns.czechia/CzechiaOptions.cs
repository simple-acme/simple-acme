using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Services.Serialization;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [JsonSerializable(typeof(CzechiaOptions))]
    internal partial class CzechiaJson : JsonSerializerContext
    {
        public CzechiaJson(WacsJsonPluginsOptionsFactory optionsFactory) : base(optionsFactory.Options) { }
    }

    public class CzechiaOptions : ValidationPluginOptions
    {
        public string ApiBaseUri { get; set; } = "https://api.czechia.com/api";

            // bude uložené šifrovaně / podporuje secret store stejně jako Cloudflare
            public ProtectedString? ApiToken { get; set; }

            // v Czechia API potřebuješ znát konkrétní zónu v URL
            public string ZoneName { get; set; } = "";

            public int Ttl { get; set; } = 3600;
    }
}
