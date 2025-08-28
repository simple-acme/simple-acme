using PKISharp.WACS.Configuration;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Any
{
    internal class ScriptOptionsFactory(
        ILogService log,
        ISettings settings,
        ArgumentsInputService arguments) : PluginOptionsFactory<ScriptOptions>
    {
        private ArgumentResult<string?> Script => arguments.
            GetString<ScriptArguments>(x => x.DnsScript, x => x.ValidationScript);

        private ArgumentResult<string?> PrepareScript => arguments.
            GetString<ScriptArguments>(x => x.DnsCreateScript, x => x.ValidationPrepareScript).
            WithLabel("Preparation script").
            Validate(x => Task.FromResult(x.ValidFile(log)), "invalid file");

        private ArgumentResult<string?> PrepareScriptArguments(bool http) => arguments.
            GetString<ScriptArguments>(x => x.DnsCreateScriptArguments, x => x.ValidationPrepareScriptArguments).
            WithDefault(http ? ScriptHttp.DefaultPrepareArguments : ScriptDns.DefaultPrepareArguments).
            WithLabel("Arguments").
            WithDescription("Arguments passed to the preparation script.").
            DefaultAsNull();

        private ArgumentResult<string?> CleanupScript => arguments.
            GetString<ScriptArguments>(x => x.DnsDeleteScript, x => x.ValidationCleanupScript).
            WithLabel("Cleanup script").
            Validate(x => Task.FromResult(x.ValidFile(log)), "invalid file");

        private ArgumentResult<string?> CleanupScriptArguments(bool http) => arguments.
            GetString<ScriptArguments>(x => x.DnsDeleteScriptArguments, x => x.ValidationCleanupScriptArguments).
            WithDefault(http ? ScriptHttp.DefaultCleanupArguments : ScriptDns.DefaultCleanupArguments).
            WithLabel("Arguments").
            WithDescription("Arguments passed to the cleanup script.").
            DefaultAsNull();

        private ArgumentResult<int?> Parallelism => arguments.
            GetInt<ScriptArguments>(x => x.DnsScriptParallelism, x => x.ValidationScriptParallelism).
            WithDefault(0).
            Validate(x => Task.FromResult(x!.Value is >= 0 and <= 3), "invalid value").
            DefaultAsNull();

        /// <summary>
        /// Interactive
        /// </summary>
        /// <param name="input"></param>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        public override async Task<ScriptOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            var ret = new ScriptOptions
            {
                ChallengeType =
                    await input.ChooseFromMenu(
                        "Which type of challenges does your script handle?",
                        [
                            Choice.Create(Constants.Http01ChallengeType, "HTTP challenge (serve file with specific name and content)"),
                            Choice.Create(Constants.Dns01ChallengeType, "DNS challenge (create TXT record with specific value)", @default: true)
                        ])
            };

            var http = ret.ChallengeType == Constants.Http01ChallengeType;
            var createScript = await PrepareScript.Interactive(input).GetValue();
            string? deleteScript = null;
            var chosen = await input.ChooseFromMenu(
                "How to clean up after validation",
                [
                    Choice.Create(() => { deleteScript = createScript; return Task.CompletedTask; }, "Using the same script"),
                    Choice.Create<Func<Task>>(async () => deleteScript = await CleanupScript.Interactive(input).GetValue(), "Using a different script"),
                    Choice.Create(() => Task.CompletedTask, "Do not clean up")
                ]);
            await chosen.Invoke();

            ProcessScripts(ret, null, createScript, deleteScript);

            input.CreateSpace();
            if (http) {
                ScriptHttp.ExplainReplacements(input);
            } else { 
                ScriptDns.ExplainReplacements(input); 
            }
            input.CreateSpace();
          
            ret.CreateScriptArguments = await PrepareScriptArguments(http).Interactive(input).GetValue();
            if (!string.IsNullOrWhiteSpace(ret.DeleteScript) || !string.IsNullOrWhiteSpace(ret.Script))
            {
                ret.DeleteScriptArguments = await CleanupScriptArguments(http).Interactive(input).GetValue();
            }
            if (!settings.Validation.DisableMultiThreading)
            {
                ret.Parallelism = await input.ChooseFromMenu(
                    "Enable parallel execution?",
                    [
                        Choice.Create<int?>(null, "Run everything one by one", @default: true),
                        Choice.Create<int?>(1, "Allow multiple instances of the script to run at the same time"),
                        Choice.Create<int?>(2, "Allow multiple domains to be validated at the same time"),
                        Choice.Create<int?>(3, "Allow both modes of parallelism")
                    ]);
            }

            return ret;
        }

        public override async Task<ScriptOptions?> Default()
        {
            var ret = new ScriptOptions();
            var commonScript = await Script.GetValue();
            var createScript = await PrepareScript.GetValue();
            var deleteScript = await CleanupScript.GetValue();
            if (!ProcessScripts(ret, commonScript, createScript, deleteScript))
            {
                return null;
            }
            ret.DeleteScriptArguments = await CleanupScriptArguments(false).GetValue();
            ret.CreateScriptArguments = await PrepareScriptArguments(false).GetValue();
            ret.Parallelism = await Parallelism.GetValue();
            return ret;
        }

        /// <summary>
        /// Choose the right script to run
        /// </summary>
        /// <param name="options"></param>
        /// <param name="commonInput"></param>
        /// <param name="createInput"></param>
        /// <param name="deleteInput"></param>
        private bool ProcessScripts(ScriptOptions options, string? commonInput, string? createInput, string? deleteInput)
        {
            if (!string.IsNullOrWhiteSpace(commonInput))
            {
                if (!string.IsNullOrWhiteSpace(createInput))
                {
                    log.Warning($"Ignoring --validationpreparescript because --validationscript was provided");
                }
                if (!string.IsNullOrWhiteSpace(deleteInput))
                {
                    log.Warning("Ignoring --validationcleanupscript because --validationscript was provided");
                }
            }
            if (string.IsNullOrWhiteSpace(commonInput) &&
                string.Equals(createInput, deleteInput, StringComparison.CurrentCultureIgnoreCase))
            {
                commonInput = createInput;
            }
            if (!string.IsNullOrWhiteSpace(commonInput))
            {
                options.Script = commonInput;
            }
            else
            {
                options.CreateScript = string.IsNullOrWhiteSpace(createInput) ? null : createInput;
                options.DeleteScript = string.IsNullOrWhiteSpace(deleteInput) ? null : deleteInput;
            }
            if (options.CreateScript == null && options.Script == null)
            {
                log.Error("Missing --validationscript or --validationpreparescript");
                return false;
            }
            return true;
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(ScriptOptions options)
        {
            var http = options.ChallengeType == Constants.Http01ChallengeType;
            yield return (Script.Meta, options.Script);
            yield return (PrepareScript.Meta, options.CreateScript);
            yield return (PrepareScriptArguments(http).Meta, options.CreateScriptArguments);
            yield return (CleanupScript.Meta, options.DeleteScript);
            yield return (CleanupScriptArguments(http).Meta, options.DeleteScriptArguments);
            yield return (Parallelism.Meta, options.Parallelism);
        }
    }
}