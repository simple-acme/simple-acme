using PKISharp.WACS.Configuration;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    internal class PfxFileOptionsFactory(
        ILogService log,
        ISettings settings,
        ArgumentsInputService arguments) : PluginOptionsFactory<PfxFileOptions>
    {
        private ArgumentResult<ProtectedString?> Password => arguments.
            GetProtectedString<PfxFileArguments>(args => args.PfxPassword, true).
            WithDefault(PfxFile.DefaultPassword(settings).Protect(true)).
            DefaultAsNull();

        private ArgumentResult<string?> Path => arguments.
            GetString<PfxFileArguments>(args => args.PfxFilePath).
            WithDefault(PfxFile.DefaultPath(settings)).
            Required().
            Validate(x => Task.FromResult(x.ValidPath(log)), "invalid path").
            DefaultAsNull();

        private ArgumentResult<string?> Name => arguments.
            GetString<PfxFileArguments>(args => args.PfxFileName);

        public override async Task<PfxFileOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            var path = await Path.Interactive(input).WithLabel("File path").GetValue();
            var name = await Name.GetValue();
            var password = await Password.Interactive(input).GetValue();
            return Create(path, name, password);
        }

        public override async Task<PfxFileOptions?> Default()
        {
            var path = await Path.GetValue();
            var name = await Name.GetValue();
            var password = await Password.GetValue();
            return Create(path, name, password);
        }

        private static PfxFileOptions Create(string? path, string? name, ProtectedString? password)
        {
            return new PfxFileOptions
            {
                PfxPassword = password,
                Path = path,
                FileName = name
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(PfxFileOptions options)
        {
            yield return (Path.Meta, options.Path);
            yield return (Name.Meta, options.FileName);
            yield return (Password.Meta, options.PfxPassword);
        }
    }

}