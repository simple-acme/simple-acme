﻿using PKISharp.WACS.Configuration;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    internal class ScriptOptionsFactory(
        ILogService log,
        ISettings settings,
        ArgumentsInputService arguments) : PluginOptionsFactory<ScriptOptions>
    {
        private ArgumentResult<string?> CommonScript => arguments.
            GetString<ScriptArguments>(x => x.DnsScript);

        private ArgumentResult<string?> CreateScript => arguments.
            GetString<ScriptArguments>(x => x.DnsCreateScript).
            Validate(x => Task.FromResult(x.ValidFile(log)), "invalid file");

        private ArgumentResult<string?> CreateScriptArguments => arguments.
            GetString<ScriptArguments>(x => x.DnsCreateScriptArguments).
            WithDefault(Script.DefaultCreateArguments).
            DefaultAsNull();

        private ArgumentResult<string?> DeleteScript => arguments.
            GetString<ScriptArguments>(x => x.DnsDeleteScript).
            Validate(x => Task.FromResult(x.ValidFile(log)), "invalid file");

        private ArgumentResult<string?> DeleteScriptArguments => arguments.
            GetString<ScriptArguments>(x => x.DnsDeleteScriptArguments).            
            WithDefault(Script.DefaultDeleteArguments).
            DefaultAsNull();

        private ArgumentResult<int?> Parallelism => arguments.
            GetInt<ScriptArguments>(x => x.DnsScriptParallelism).
            WithDefault(0).
            Validate(x => Task.FromResult(x!.Value is >= 0 and <= 3), "invalid value").
            DefaultAsNull();

        public override async Task<ScriptOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            var ret = new ScriptOptions();
            var createScript = await CreateScript.Interactive(input).GetValue();
            string? deleteScript = null;
            var chosen = await input.ChooseFromMenu(
                "How to delete records after validation",
                new List<Choice<Func<Task>>>()
                {
                    Choice.Create<Func<Task>>(() => {
                        deleteScript = createScript;
                        return Task.CompletedTask;
                    }, "Using the same script"),
                    Choice.Create<Func<Task>>(async () => 
                        deleteScript = await DeleteScript.Interactive(input).Required().GetValue()
                    , "Using a different script"),
                    Choice.Create<Func<Task>>(() => Task.CompletedTask, "Do not delete")
                });
            await chosen.Invoke();

            ProcessScripts(ret, null, createScript, deleteScript);

            input.CreateSpace();
            input.Show("{Identifier}", "Domain that's being validated, e.g. sub.example.com");
            input.Show("{RecordName}", "Full name of the TXT record that is required, e.g. _acme-challenge.sub.example.com");
            input.Show("{ZoneName}", "Registerable domain, e.g. example.com");
            input.Show("{NodeName}", "Full subdomain, e.g. _acme-challenge.sub");
            input.Show("{Token}", "Token that should be the content of the TXT record");
            input.Show("{vault://json/mysecret}", "Secret from the vault");
            input.CreateSpace();
            ret.CreateScriptArguments = await CreateScriptArguments.Interactive(input).GetValue();
            if (!string.IsNullOrWhiteSpace(ret.DeleteScript) || !string.IsNullOrWhiteSpace(ret.Script))
            {
                ret.DeleteScriptArguments = await DeleteScriptArguments.Interactive(input).GetValue();
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
            ret.DeleteScriptArguments = await DeleteScriptArguments.GetValue();
            ret.CreateScriptArguments = await CreateScriptArguments.GetValue();
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
            yield return (CreateScript.Meta, options.CreateScript);
            yield return (CreateScriptArguments.Meta, options.CreateScriptArguments);
            yield return (DeleteScript.Meta, options.DeleteScript);
            yield return (DeleteScriptArguments.Meta, options.DeleteScriptArguments);
            yield return (Parallelism.Meta, options.Parallelism);
        }
    }
}