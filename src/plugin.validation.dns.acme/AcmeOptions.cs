using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    [JsonSerializable(typeof(AcmeOptions))]
    [JsonSerializable(typeof(UpdateRequest))]
    [JsonSerializable(typeof(RegisterResponse))]
    internal partial class AcmeJson : JsonSerializerContext
    {
        public AcmeJson(WacsJsonPluginsOptionsFactory optionsFactory) : base(optionsFactory.Options)
        {
        }
    }

    public class AcmeOptions : ValidationPluginOptions
    {
        public string? BaseUri { get; set; }
        public NetworkCredentialOptions? Credential { get; set; }

        public override void Show(IInputService input)
        {
            base.Show(input);
            Credential?.Show(input);
        }
    }
}