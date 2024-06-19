using PKISharp.WACS.Configuration;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.TargetPlugins
{
    internal class CsrOptionsFactory(ILogService log, ArgumentsInputService arguments) : PluginOptionsFactory<CsrOptions>
    {
        public override int Order => 6;

        private ArgumentResult<string?> CsrFile => arguments.
            GetString<CsrArguments>(x => x.CsrFile).
            Required().
            Validate(x => Task.FromResult(x.ValidFile(log)), "invalid file");

        private ArgumentResult<string?> PkFile => arguments.
            GetString<CsrArguments>(x => x.PkFile).
            Validate(x => Task.FromResult(x.ValidFile(log)), "invalid file");

        public override async Task<CsrOptions?> Aquire(IInputService inputService, RunLevel runLevel)
        {
            return new CsrOptions()
            {
                PkFile = await PkFile.Interactive(inputService).GetValue(),
                CsrFile = await CsrFile.Interactive(inputService).GetValue()
            };
        }

        public override async Task<CsrOptions?> Default()
        {
            return new CsrOptions()
            {
                PkFile = await PkFile.GetValue(),
                CsrFile = await CsrFile.GetValue()
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(CsrOptions options)
        {
            yield return (CsrFile.Meta, options.CsrFile);
            yield return (PkFile.Meta, options.PkFile);
        }
    }
}
