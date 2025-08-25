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
        private ArgumentResult<string?> CommonScript => arguments.
            GetString<ScriptArguments>(x => x.DnsScript, x => x.Script);

        private ArgumentResult<string?> CreateScript => arguments.
            GetString<ScriptArguments>(x => x.DnsCreateScript).
            WithLabel("Preparation script").
            Validate(x => Task.FromResult(x.ValidFile(log)), "invalid file");

        private ArgumentResult<string?> CreateScriptArguments(bool http) => arguments.
            GetString<ScriptArguments>(x => x.DnsCreateScriptArguments).
            WithDefault(http ? ScriptHttp.DefaultCreateArguments : ScriptDns.DefaultCreateArguments).
            WithLabel("Arguments").
            WithDescription("Arguments passed to the preparation script.").
            DefaultAsNull();

        private ArgumentResult<string?> DeleteScript => arguments.
            GetString<ScriptArguments>(x => x.DnsDeleteScript).
            WithLabel("Cleanup script").
            Validate(x => Task.FromResult(x.ValidFile(log)), "invalid file");

        private ArgumentResult<string?> DeleteScriptArguments(bool http) => arguments.
            GetString<ScriptArguments>(x => x.DnsDeleteScriptArguments).
            WithDefault(http ? ScriptHttp.DefaultCreateArguments : ScriptDns.DefaultCreateArguments).
            WithLabel("Arguments").
            WithDescription("Arguments passed to the cleanup script.").
            DefaultAsNull();

        private ArgumentResult<int?> Parallelism => arguments.
            GetInt<ScriptArguments>(x => x.DnsScriptParallelism).
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
                        "Which type of challenge does your script handle?",
                        [
                            Choice.Create(Constants.Http01ChallengeType, "HTTP challenge (serve file with specific name and content)"),
                            Choice.Create(Constants.Dns01ChallengeType, "DNS challenge (create TXT record with specific value)", @default: true)
                        ])
            };

            var http = ret.ChallengeType == Constants.Http01ChallengeType;
            var createScript = await CreateScript.Interactive(input).GetValue();
            string? deleteScript = null;
            var chosen = await input.ChooseFromMenu(
                "How to clean up after validation",
                [
                    Choice.Create(() => { deleteScript = createScript; return Task.CompletedTask; }, "Using the same script"),
                    Choice.Create<Func<Task>>(async () => deleteScript = await DeleteScript.Interactive(input).GetValue(), "Using a different script"),
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
          
            ret.CreateScriptArguments = await CreateScriptArguments(http).Interactive(input).GetValue();
            if (!string.IsNullOrWhiteSpace(ret.DeleteScript) || !string.IsNullOrWhiteSpace(ret.Script))
            {
                ret.DeleteScriptArguments = await DeleteScriptArguments(http).Interactive(input).GetValue();
            }
            if (!settings.Validation.DisableMultiThreading)
            {
                ret.Parallelism = await input.ChooseFromMenu(
                    "Enable parallel execution?",
                    [
                        Choice.Create<int?>(null, "Run everything one by one", @default: true),
                        Choice.Create<int?>(1, "Allow multiple instances of the script to run at the same time"),
                        Choice.Create<int?>(2, "Allow multiple records to be validated at the same time"),
                        Choice.Create<int?>(3, "Allow both modes of parallelism")
                    ]);
            }

            return ret;
        }

        public override async Task<ScriptOptions?> Default()
        {
            var ret = new ScriptOptions();
            var commonScript = await CommonScript.GetValue();
            var createScript = await CreateScript.GetValue();
            var deleteScript = await DeleteScript.GetValue();
            if (!ProcessScripts(ret, commonScript, createScript, deleteScript))
            {
                return null;
            }
            ret.DeleteScriptArguments = await DeleteScriptArguments(false).GetValue();
            ret.CreateScriptArguments = await CreateScriptArguments(false).GetValue();
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
                    log.Warning($"Ignoring --dnscreatescript because --dnsscript was provided");
                }
                if (!string.IsNullOrWhiteSpace(deleteInput))
                {
                    log.Warning("Ignoring --dnsdeletescript because --dnsscript was provided");
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
                log.Error("Missing --dnsscript or --dnscreatescript");
                return false;
            }
            return true;
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(ScriptOptions options)
        {
            var http = options.ChallengeType == Constants.Http01ChallengeType;
            yield return (CommonScript.Meta, options.Script);
            yield return (CreateScript.Meta, options.CreateScript);
            yield return (CreateScriptArguments(http).Meta, options.CreateScriptArguments);
            yield return (DeleteScript.Meta, options.DeleteScript);
            yield return (DeleteScriptArguments(http).Meta, options.DeleteScriptArguments);
            yield return (Parallelism.Meta, options.Parallelism);
        }
    }
}