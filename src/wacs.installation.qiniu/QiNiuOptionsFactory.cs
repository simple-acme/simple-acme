using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.InstallationPlugins
{
    internal class QiNiuOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<QiNiuOptions>
    {

        private ArgumentResult<ProtectedString?> AccessKey => arguments.
            GetProtectedString<QiNiuArguments>(x => x.AccessKey).
            Required();

        private ArgumentResult<ProtectedString?> SecretKey => arguments.
            GetProtectedString<QiNiuArguments>(x => x.SecretKey).
            Required();

        private ArgumentResult<string?> QiNiuServer => arguments.
            GetString<QiNiuArguments>(x => x.QiNiuServer).
            Required();

        public override async Task<QiNiuOptions?> Aquire(IInputService inputService, RunLevel runLevel)
        {
            return new QiNiuOptions
            {
                QiNiuServer = await QiNiuServer.Interactive(inputService).WithLabel("Qiniu Cloud Server").GetValue(),
                AccessKey = await AccessKey.Interactive(inputService).WithLabel("Qiniu Cloud AccessKey").GetValue(),
                SecretKey = await SecretKey.Interactive(inputService).WithLabel("Qiniu Cloud SecretKey").GetValue(),
            };
        }

        public override async Task<QiNiuOptions?> Default()
        {
            return new QiNiuOptions
            {
                QiNiuServer = await QiNiuServer.GetValue(),
                AccessKey = await AccessKey.GetValue(),
                SecretKey = await SecretKey.GetValue(),
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(QiNiuOptions options)
        {
            yield return (QiNiuServer.Meta, options.QiNiuServer);
            yield return (AccessKey.Meta, options.AccessKey);
            yield return (SecretKey.Meta, options.SecretKey);
        }
    }
}
