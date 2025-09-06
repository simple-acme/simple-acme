using PKISharp.WACS.Configuration;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    internal class PemFilesOptionsFactory(
        ILogService log,
        ISettings settings,
        ArgumentsInputService arguments) : PluginOptionsFactory<PemFilesOptions>
    {
        private ArgumentResult<ProtectedString?> Password => arguments.
            GetProtectedString<PemFilesArguments>(args => args.PemPassword, true).
            WithDefault(settings.Store.PemFiles.DefaultPassword.Protect(true)).
            DefaultAsNull();

        private ArgumentResult<string?> Path => arguments.
            GetString<PemFilesArguments>(args => args.PemFilesPath).
            WithDefault(settings.Store.PemFiles.DefaultPath).
            Required().
            Validate(x => Task.FromResult(x.ValidPath(log)), "invalid path").
            DefaultAsNull();

        private ArgumentResult<string?> Name => arguments.
            GetString<PemFilesArguments>(args => args.PemFilesName);

        public override async Task<PemFilesOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            var path = await Path.Interactive(input).WithLabel("File path").GetValue();
            var name = await Name.GetValue();
            var password = await Password.Interactive(input).GetValue();
            return Create(path, name, password);
        }

        public override async Task<PemFilesOptions?> Default()
        {
            var path = await Path.GetValue();
            var name = await Name.GetValue();
            var password = await Password.GetValue();
            return Create(path, name, password);
        }

        private static PemFilesOptions Create(
            string? path, 
            string? name,
            ProtectedString? password)
        {
            return new PemFilesOptions
            {
                PemPassword = password,
                Path = path,
                FileName = name
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(PemFilesOptions options)
        {
            yield return (Path.Meta, options.Path);
            yield return (Password.Meta, options.PemPassword);
        }
    }

}