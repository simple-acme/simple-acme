using Autofac;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.Resolvers;
using PKISharp.WACS.Plugins.TargetPlugins;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="input"></param>
    internal partial class ValidationOptionsService(
        IInputService input,
        ILogService log,
        ISettings settings,
        IAutofacBuilder autofac,
        WacsJson wacsJson) : IValidationOptionsService
    {
        private readonly IInputService _input = input;
        private GlobalValidationPluginOptionsCollection _data = new();
        private bool _loaded = false;

        /// <summary>
        /// File where the validation information is stored
        /// </summary>
        private FileInfo Store => new(Path.Join(settings.Client.ConfigurationPath, "validation.json"));

        /// <summary>
        /// Current data
        /// </summary>
        private async Task<IEnumerable<GlobalValidationPluginOptions>> GlobalOptions()
        {
            if (!_loaded)
            {
                await Load();
            }
            return _data.Options?.OrderBy(o => o.Priority).ToList() ?? [];
        }

        /// <summary>
        /// Re-save with new encryption setting applied
        /// </summary>
        /// <returns></returns>
        public async Task Encrypt()
        {
            await Load();
            await Save();
        }

        /// <summary>
        /// Save to disk
        /// </summary>
        /// <returns></returns>
        private async Task Save()
        {
            if (_data.Options == null || _data.Options.Count == 0)
            {
                if (Store.Exists)
                {
                    Store.Delete();
                }
                return;
            }
            var rawJson = JsonSerializer.Serialize(_data, wacsJson.GlobalValidationPluginOptionsCollection);
            if (!string.IsNullOrWhiteSpace(rawJson))
            {
                await Store.SafeWrite(rawJson);
            }
        }

        /// <summary>
        /// Load from disk
        /// </summary>
        /// <returns></returns>
        private async Task Load()
        {
            if (!Store.Exists)
            {
                return;
            }
            try
            {
                var rawJson = await File.ReadAllTextAsync(Store.FullName);
                var data = default(GlobalValidationPluginOptionsCollection);
                if (rawJson.StartsWith('['))
                {
                    // Backwards compatible format with a list
                    var list = JsonSerializer.Deserialize(rawJson, wacsJson.ListGlobalValidationPluginOptions) ?? throw new Exception("invalid data");
                    data = new GlobalValidationPluginOptionsCollection() { Options = list };
                }
                else
                {
                    // Modern format with schema
                    data = JsonSerializer.Deserialize(rawJson, wacsJson.GlobalValidationPluginOptionsCollection) ?? throw new Exception("invalid data");
                }
                if (data != null)
                {
                    _data = data;
                    _loaded = true;
                }
            }
            catch (Exception ex)
            {
                log.Error(ex, "Unable to read global validation options from {path}", Store.FullName);
            }
        }

        /// <summary>
        /// Manage validation options
        /// </summary>
        /// <returns></returns>
        public async Task Manage(ILifetimeScope scope)
        {
            // Pick bindings
            var exit  = false;
            while (!exit)
            {
                _input.CreateSpace();
                _input.Show(null, "Welcome to the global validation options manager. Here you may " +
                    "define validation options that will be prioritized over the settings chosen for " +
                    "individual renewals. This can ease management when there are many renewals " +
                    "(e.g. when rotating credentials or switching DNS providers), but it also enables you " +
                    "to create certificates where different domains have different validation requirements. " +
                    "If you are not sure why you might need this, just go back and stick with regular renewals.");

                var options = await GlobalOptions();

                var menu = options.
                    Select(o => Choice.Create(() => Edit(scope, o),
                        $"Manage validation settings for {o.Pattern ?? o.Regex} (priority {o.Priority})")).ToList();

                menu.Add(Choice.Create(() => Add(scope),
                        "Add new global validation setting", command: "A"));

                menu.Add(Choice.Create(() => { exit = true; return Task.CompletedTask; },
                        "Back", @default: true, command: "Q"));

                var chosen = await _input.ChooseFromMenu("Choose menu option", menu);
                await chosen.Invoke();
            }
        }

        /// <summary>
        /// Change options for a previously created instance
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        private async Task Edit(ILifetimeScope scope, GlobalValidationPluginOptions options)
        {
            // Change properties
            var exit = false;
            var save = false;
            while (!exit)
            {
                var menu = new List<Choice<Func<Task>>>
                {
                    Choice.Create(() => UpdatePriority(options), "Change priority"),
                    Choice.Create(() => UpdatePattern(options), "Change pattern"),
                    Choice.Create(() => UpdateOptions(scope, options, RunLevel.Interactive | RunLevel.Advanced), "Change settings"),
                    Choice.Create(() => { exit = true; save = false; return Delete(options); }, "Delete"),
                    Choice.Create(() => { exit = true; save = false; return Task.CompletedTask; }, "Cancel", command: "C"),
                    Choice.Create(() => { exit = true; save = true; return Task.CompletedTask; }, "Save and quit", @default: true, command: "Q")
                };
                var chosen = await _input.ChooseFromMenu("Choose menu option", menu);
                await chosen.Invoke();
            }
            if (save)
            {
                await Save();
            }
            else
            {
                // Reload from disk to undo changes
                await Load();
            }
        }

        /// <summary>
        /// Set the priority
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private async Task UpdatePriority(GlobalValidationPluginOptions input) => 
            input.Priority = await _input.RequestInt("Priority");

        /// <summary>
        /// Set the pattern
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private async Task UpdatePattern(GlobalValidationPluginOptions input)
        {
            _input.Show(null, IISArguments.PatternExamples);
            string pattern;
            do
            {
                pattern = await _input.RequestString("Pattern");
            }
            while (!IISOptionsFactory.ParsePattern(pattern, log));
            input.Pattern = pattern;
        }

        /// <summary>
        /// Update the validation options
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="input"></param>
        /// <returns></returns>
        private async Task UpdateOptions(ILifetimeScope scope, GlobalValidationPluginOptions input, RunLevel runLevel)
        {
            var dummy = new Target(new DnsIdentifier("www.example.com"));
            var target = autofac.Target(scope, dummy);
            IResolver resolver = runLevel.HasFlag(RunLevel.Unattended) ?
                    scope.Resolve<UnattendedResolver>(new TypedParameter(typeof(ILifetimeScope), target)) : 
                    scope.Resolve<InteractiveResolver>(new TypedParameter(typeof(ILifetimeScope), target), new TypedParameter(typeof(RunLevel), runLevel));

            var validationPlugin = await resolver.GetValidationPlugin();
            if (validationPlugin != null)
            {
                input.ValidationPluginOptions = runLevel.HasFlag(RunLevel.Unattended)
                    ? await validationPlugin.OptionsFactory.Default()
                    : await validationPlugin.OptionsFactory.Aquire(_input, runLevel);
            }
        }

        /// <summary>
        /// Delete options from the list
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private async Task Delete(GlobalValidationPluginOptions input)
        {
            _data.Options = _data.Options?.Except([input]).ToList();
            await Save();
        }

        /// <summary>
        /// Configure new instance interactively
        /// </summary>
        /// <returns></returns>
        public async Task Add(ILifetimeScope scope)
        {
            _input.CreateSpace();
            var global = new GlobalValidationPluginOptions();
            await UpdatePattern(global);
            await UpdateOptions(scope, global, RunLevel.Interactive | RunLevel.Advanced);
            await UpdatePriority(global);
            _data.Options ??= [];
            _data.Options.Add(global);
            await Save();
        }

        /// <summary>
        /// Configure/update instance from the command line
        /// </summary>
        /// <param name="scope"></param>
        /// <param name="pattern"></param>
        /// <param name="priority"></param>
        /// <returns></returns>
        public async Task Add(ILifetimeScope scope, string? pattern, int? priority)
        {
            if (!IISOptionsFactory.ParsePattern(pattern, log))
            {
                throw new ArgumentException("Pattern is not valid", nameof(pattern));
            }

            var options = await GlobalOptions();
            var option = options.Where(o => o.Pattern == pattern).FirstOrDefault();
            var newOption = false;
            if (option != null)
            {
               log.Information("A global validation option with pattern {pattern} already exists, updating", pattern);
            }
            else
            {
                log.Information("Creating new global validation option with pattern {pattern}", pattern);
                option = new GlobalValidationPluginOptions() { Pattern = pattern };
                newOption = true;
            }
            option.Priority = priority ?? int.MaxValue;
            await UpdateOptions(scope, option, RunLevel.Unattended);
            if (option.ValidationPluginOptions == null)
            {
                throw new Exception("Unable to configure global validation option");
            }
            if (newOption)
            {
                _data.Options ??= [];
                _data.Options.Add(option);
            }
            await Save();
        }

        /// <summary>
        /// Accessed by the renewal process
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public async Task<ValidationPluginOptions?> GetValidationOptions(Identifier identifier)
        {
            var options = await GlobalOptions();
            return options.
                Where(o => o.Match(identifier)).
                FirstOrDefault()?.
                ValidationPluginOptions;
        }
    }
}
