using PKISharp.WACS.Configuration;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    internal class P7bFileOptionsFactory(
        ILogService log,
        ISettings settings,
        ArgumentsInputService arguments) : PluginOptionsFactory<P7bFileOptions>
    {
        private ArgumentResult<string?> Path => arguments.
            GetString<P7bFileArguments>(args => args.P7bFilePath).
            WithDefault(P7bFile.DefaultPath(settings)).
            Required().
            Validate(x => Task.FromResult(x.ValidPath(log)), "invalid path").
            DefaultAsNull();

        private ArgumentResult<string?> Name => arguments.
            GetString<P7bFileArguments>(args => args.P7bFileName);

        public override async Task<P7bFileOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            var path = await Path.Interactive(input, "File path").GetValue();
            var name = await Name.GetValue();
            return Create(path, name);
        }

        public override async Task<P7bFileOptions?> Default()
        {
            var path = await Path.GetValue();
            var name = await Name.GetValue();
            return Create(path, name);
        }

        private static P7bFileOptions Create(string? path, string? name)
        {
            return new P7bFileOptions
            {
                Path = path,
                FileName = name
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(P7bFileOptions options)
        {
            yield return (Path.Meta, options.Path);
            yield return (Name.Meta, options.FileName);
        }
    }

}