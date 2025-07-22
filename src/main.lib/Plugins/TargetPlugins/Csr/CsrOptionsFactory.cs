using PKISharp.WACS.Configuration;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.TargetPlugins
{
    internal class CsrOptionsFactory(ILogService log, ArgumentsInputService arguments) : PluginOptionsFactory<CsrOptions>
    {
        public override int Order => 6;

        private ArgumentResult<string?> CsrFile => arguments.
            GetString<CsrArguments>(x => x.CsrFile).
            Validate(x => Task.FromResult(x.ValidFile(log)), "invalid file");

        private ArgumentResult<string?> CsrScript => arguments.
            GetString<CsrArguments>(x => x.CsrScript).
            Validate(x => Task.FromResult(x.ValidFile(log)), "invalid file");

        private ArgumentResult<string?> PkFile => arguments.
            GetString<CsrArguments>(x => x.PkFile).
            Validate(x => Task.FromResult(x.ValidFile(log)), "invalid file");

        public override async Task<CsrOptions?> Aquire(IInputService inputService, RunLevel runLevel)
        {
            var options = new List<Choice<string>>
            {
                Choice.Create("file", "Static file"),
                Choice.Create("script", "Dynamic script")
            };
            var chosen = await inputService.ChooseFromMenu("Where will the CSR come from?", options);
            if (chosen == "file")
            {
                return new CsrOptions()
                {
                    PkFile = await PkFile.Interactive(inputService).GetValue(),
                    CsrFile = await CsrFile.Interactive(inputService).Required().GetValue()
                };
            }
            else
            {
                return new CsrOptions()
                {
                    CsrScript = await CsrScript.Interactive(inputService).Required().GetValue()
                };
            }
        }

        public override async Task<CsrOptions?> Default()
        {
            var ret = new CsrOptions()
            {
                PkFile = await PkFile.GetValue(),
                CsrFile = await CsrFile.GetValue(),
                CsrScript = await CsrScript.GetValue()
            };
            if (string.IsNullOrWhiteSpace(ret.CsrFile) && string.IsNullOrEmpty(ret.CsrScript))
            {
                throw new InvalidOperationException("You must specify either --csrfile or --csrscript");
            }
            return ret;
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(CsrOptions options)
        {
            yield return (CsrFile.Meta, options.CsrFile);
            yield return (CsrScript.Meta, options.CsrScript);
            yield return (PkFile.Meta, options.PkFile);
        }
    }
}
