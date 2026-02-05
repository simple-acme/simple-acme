using PKISharp.WACS.Configuration;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    internal class QiNiuOptionsFactory(
        ISettings settings,
        ArgumentsInputService arguments) : PluginOptionsFactory<QiNiuOptions>
    {
        private ArgumentResult<ProtectedString?> Password => arguments.
            GetProtectedString<QiNiuArguments>(args => args.Password, true).
            WithDefault(settings.Store.PemFiles.DefaultPassword.Protect(true)).
            DefaultAsNull();


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
                Password = await Password.Interactive(inputService).WithLabel("Password to protect the PEM").GetValue(),
                QiNiuServer = await QiNiuServer.Interactive(inputService).WithLabel("Qiniu Cloud Server").GetValue(),
                AccessKey = await AccessKey.Interactive(inputService).WithLabel("Qiniu Cloud AccessKey").GetValue(),
                SecretKey = await SecretKey.Interactive(inputService).WithLabel("Qiniu Cloud SecretKey").GetValue(),
            };
        }

        public override async Task<QiNiuOptions?> Default()
        {
            return new QiNiuOptions
            {
                Password = await Password.GetValue(),
                QiNiuServer = await QiNiuServer.GetValue(),
                AccessKey = await AccessKey.GetValue(),
                SecretKey = await SecretKey.GetValue(),
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(QiNiuOptions options)
        {
            yield return (Password.Meta, options.Password);
            yield return (QiNiuServer.Meta, options.QiNiuServer);
            yield return (AccessKey.Meta, options.AccessKey);
            yield return (SecretKey.Meta, options.SecretKey);
        }
    }

}