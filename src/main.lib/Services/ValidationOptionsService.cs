using Autofac;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Plugins.Resolvers;
using PKISharp.WACS.Plugins.TargetPlugins;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="input"></param>
    internal class ValidationOptionsService(
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
                try
                {
                    // Modern format with schema
                    data = JsonSerializer.Deserialize(rawJson, wacsJson.GlobalValidationPluginOptionsCollection) ?? throw new Exception("invalid data");
                } 
                catch
                {
                    // Backwards compatible format
                    var list = JsonSerializer.Deserialize(rawJson, wacsJson.ListGlobalValidationPluginOptions) ?? throw new Exception("invalid data");
                    data = new GlobalValidationPluginOptionsCollection() { Options = list };
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
                    Choice.Create(() => UpdateOptions(scope, options), "Change settings"),
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
        private async Task UpdateOptions(ILifetimeScope scope, GlobalValidationPluginOptions input)
        {
            var dummy = new Target(new DnsIdentifier("www.example.com"));
            var target = autofac.Target(scope, dummy);
            var resolver = scope.Resolve<InteractiveResolver>(
                new TypedParameter(typeof(ILifetimeScope), target),
                new TypedParameter(typeof(RunLevel), RunLevel.Advanced));
            var validationPlugin = await resolver.GetValidationPlugin();
            if (validationPlugin != null)
            {
                var options = await validationPlugin.OptionsFactory.Aquire(_input, RunLevel.Advanced);
                input.ValidationPluginOptions = options;
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
        /// Configure new instance
        /// </summary>
        /// <returns></returns>
        public async Task Add(ILifetimeScope scope)
        {
            _input.CreateSpace();
            var global = new GlobalValidationPluginOptions();
            await UpdatePattern(global);
            await UpdateOptions(scope, global);
            await UpdatePriority(global);
            _data.Options ??= [];
            _data.Options.Add(global);
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

        public class GlobalValidationPluginOptionsCollection
        {
            [JsonPropertyName("$schema")]
            public string Schema { get; set; } = "https://simple-acme.com/schema/validation.json";
            public List<GlobalValidationPluginOptions>? Options { get; set; }
        }

        /// <summary>
        /// Serialized data
        /// </summary>
        public class GlobalValidationPluginOptions
        {
            /// <summary>
            /// Priority of this rule (lower number = higher priority)
            /// </summary>
            public int? Priority { get; set; }

            /// <summary>
            /// Direct input of a regular expression
            /// </summary>
            public string? Regex { get; set; }

            /// <summary>
            /// Input of a pattern like used in other
            /// parts of the software as well, e.g.
            /// </summary>
            public string? Pattern { get; set; }

            /// <summary>
            /// The actual validation options that 
            /// are stored for re-use
            /// </summary>
            public ValidationPluginOptions? ValidationPluginOptions { get; set; }

            /// <summary>
            /// Convert the user settings into a Regex that will be 
            /// matched with the identifier.
            /// </summary>
            private Regex? ParsedRegex()
            {
                if (Pattern != null)
                {
                    return new Regex(Pattern.PatternToRegex());
                }
                if (Regex != null)
                {
                    return new Regex(Regex);
                }
                return null;
            }

            /// <summary>
            /// Test if this specific identifier is a match
            /// for these validation options
            /// </summary>
            /// <param name="identifier"></param>
            /// <returns></returns>
            public bool Match(Identifier identifier)
            {
                var regex = ParsedRegex();
                if (regex == null)
                {
                    return false;
                }
                return regex.IsMatch(identifier.Value);
            }
        }
    }
}
