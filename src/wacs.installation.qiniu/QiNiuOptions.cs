using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Text.Json.Serialization;

namespace PKISharp.WACS.Plugins.InstallationPlugins
{
    [JsonSerializable(typeof(QiNiuOptions))]
    internal partial class QiNiuJson : JsonSerializerContext
    {
        public QiNiuJson(WacsJsonPluginsOptionsFactory optionsFactory) : base(optionsFactory.Options)
        {
        }
    }
    internal class QiNiuOptions: InstallationPluginOptions
    {

        public string? QiNiuServer { get; set; }

        public ProtectedString? AccessKey { get; set; }

        public ProtectedString? SecretKey { get; set; }

    }
}
