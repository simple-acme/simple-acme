using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PKISharp.WACS.Configuration
{
    public class ArgumentsParser
    {
        private readonly ILogService _log;
        private readonly string[] _args;
        private readonly IEnumerable<IArgumentsProvider> _providers;
        private readonly IEnumerable<CommandLineAttribute> _arguments;

        public ArgumentsParser(ILogService log, AssemblyService assemblyService, string[] args)
        {
            _log = log;
            _args = args;
            _providers = ArgumentsProviders(assemblyService);
            _arguments = _providers.SelectMany(x => x.Configuration).ToList();
        }

        public IEnumerable<IArgumentsProvider> ArgumentsProviders(AssemblyService assemblyService)
        {
            var argumentGroups = assemblyService.GetResolvable<IArguments>();
            return argumentGroups.Select(x =>
                {
                    var type = typeof(BaseArgumentsProvider<>).MakeGenericType(x.Type);
                    var constr = type.GetConstructor([]) ?? throw new Exception("IArgumentsProvider should have parameterless constructor");
                    try
                    {
                        var ret = (IArgumentsProvider)constr.Invoke([]);
                        ret.Log = _log;
                        return ret;
                    }
                    catch (Exception ex)
                    {
                        if (ex.InnerException != null)
                        {
                            ex = ex.InnerException;
                        }
                        _log.Error(ex, ex.Message);
                        return null;
                    }
                }).
                OfType<IArgumentsProvider>().
                ToList();
        }

        public T? GetArguments<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, new()
        {
            foreach (var provider in _providers)
            {
                if (provider is IArgumentsProvider<T> typedProvider)
                {
                    return typedProvider.GetResult(_args);
                }
            }
            throw new InvalidOperationException($"Unable to find class that implements IArgumentsProvider<{typeof(T).Name}>");
        }

        /// <summary>
        /// Test if the arguments can be resolved by any of the known providers
        /// </summary>
        /// <returns></returns>
        internal bool Validate()
        {
            var extraOptions = _providers.First().GetExtraArguments(_args);
            foreach (var extraOption in extraOptions)
            {
                var super = _arguments.FirstOrDefault(x => string.Equals(x.Name, extraOption, StringComparison.InvariantCultureIgnoreCase));
                if (super == null)
                {
                    _log.Error("Unknown argument --{0}, use --help to get a list of possible arguments", extraOption);
                    return false;
                }
            }

            // Run indivual result validations
            var main = GetArguments<MainArguments>();
            if (main == null)
            {
                return false;
            }
            var mainProvider = _providers.OfType<IArgumentsProvider<MainArguments>>().First();
            if (mainProvider.Validate(main, main, _args))
            {
                // Validate the others
                var others = _providers.Except(new[] { mainProvider });
                foreach (var other in others)
                {
                    var opt = other.GetResult(_args);
                    if (opt == null)
                    {
                        return false;
                    }
                    if (!other.Validate(opt, main, _args))
                    {
                        return false;
                    }
                }
            }
            else
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Test if any arguments are active, so that we can warn users
        /// that these arguments have no effect on renewals.
        /// </summary>
        /// <returns></returns>
        public bool Active()
        {
            var mainProvider = _providers.OfType<IArgumentsProvider<MainArguments>>().First();
            var others = _providers.Except(new[] { mainProvider });
            foreach (var other in others)
            {
                var opt = other.GetResult(_args);
                if (opt != null && other.Active(opt, _args))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Get list of secret arguments that should be censored in the logs
        /// </summary>
        internal IEnumerable<string> SecretArguments => _arguments.Where(x => x.Secret).Select(x => x.ArgumentName);

        /// <summary>
        /// Show current command line
        /// </summary>
        internal void ShowCommandLine()
        {
            try
            {
                var censoredArgs = new List<string>();
                var censor = false;
                for (var i = 0; i < _args.Length; i++)
                {
                    if (!censor)
                    {
                        var value = _args[i];
                        value = value.Replace("\"", "\\\"");
                        if (value.Contains(' '))
                        {
                            value = $"\"{value}\"";
                        }
                        censoredArgs.Add(value);
                        censor = SecretArguments.Any(c => 
                            _args[i].Equals($"--{c}", StringComparison.CurrentCultureIgnoreCase) || 
                            _args[i].Equals($"/{c}", StringComparison.CurrentCultureIgnoreCase));
                    }
                    else
                    {
                        censoredArgs.Add("********");
                        censor = false;
                    }
                }
                var argsFormat = censoredArgs.Count != 0 ? $"Arguments: {string.Join(" ", censoredArgs)}" : "No command line arguments provided";
                _log.Verbose(argsFormat);
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Error censoring command line");
            }
        }

        /// <summary>
        /// Show command line arguments for the help function
        /// </summary>
        internal void ShowArguments()
        {
            Console.WriteLine();
            foreach (var providerGroup in _providers.GroupBy(p => p.Group).OrderBy(g => g.Key))
            {
                if (!string.IsNullOrEmpty(providerGroup.Key))
                {
                    Console.WriteLine($" ---------------------");
                    Console.WriteLine($" {providerGroup.Key}");
                    Console.WriteLine($" ---------------------");
                    Console.WriteLine();
                }

                foreach (var provider in providerGroup)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"    {provider.Name}");
                    Console.ResetColor();
                    if (!string.IsNullOrEmpty(provider.Condition))
                    {
                        Console.Write($"   [{provider.Condition}]");
                        if (provider.Default)
                        {
                            Console.WriteLine(" (default)");
                        }
                        else
                        {
                            Console.WriteLine();
                        }
                    }
                    Console.WriteLine();
                    foreach (var x in provider.Configuration.Where(x => !x.Obsolete))
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"      --{x.ArgumentName}");
                        Console.WriteLine();
                        Console.ResetColor();
                        var step = 60;
                        var pos = 0;
                        var words = x.Description?.Split(' ') ?? [];
                        while (pos < words.Length)
                        {
                            var line = "";
                            while (line == "" || (pos < words.Length && line.Length + words[pos].Length + 1 < step))
                            {
                                line += " " + words[pos++];
                            }
                            if (!Console.IsOutputRedirected)
                            {
                                Console.SetCursorPosition(3, Console.CursorTop);
                            }
                            Console.WriteLine($" {line}");
                        }
                        Console.WriteLine();
                    }
                    Console.WriteLine();
                }
            }
        }

        /// <summary>
        /// Generate YAML for documentation website
        /// </summary>
        internal void ShowArgumentsYaml()
        {
            var x = new StringBuilder();
            foreach (var providerGroup in _providers.GroupBy(p => p.Group).OrderBy(g => g.Key))
            {
                if (!string.IsNullOrEmpty(providerGroup.Key))
                {
                    x.AppendLine($"{providerGroup.Key}:");
                } 
                else
                {
                    x.AppendLine($"Main:");
                }
                foreach (var provider in providerGroup)
                {
                    x.AppendLine($"    {provider.Name}:");
                    if (provider.Condition != null)
                    {
                        x.AppendLine($"         condition: \"{EscapeYaml(provider.Condition)}\"");
                    }
                    x.AppendLine($"         arguments:");
                    foreach (var c in provider.Configuration.Where(x => !x.Obsolete))
                    {
                        x.AppendLine($"             -");
                        x.AppendLine($"                 name: {c.ArgumentName}");
                        if (c.Description != null)
                        {
                            x.AppendLine($"                 description: \"{EscapeYaml(c.Description)}\"");
                        }
                        if (c.Default != null)
                        {
                            x.AppendLine($"                 default: \"{EscapeYaml(c.Default)}\"");
                        }
                        if (c.Secret == true)
                        {
                            x.AppendLine($"                 secret: true");
                        }
                    }
                    x.AppendLine();
                }
            }
            File.WriteAllText("arguments.yaml", x.ToString());
            _log.Debug("YAML written to {0}", new FileInfo("arguments.yaml").FullName);
        }

        internal static string EscapeYaml(string input)
        {
            input = input.Replace("\"", "\\\""); // Escape quote
            input = input.Replace("--", "‑‑"); // Regular hyphen to non-breaking
            input = Regex.Replace(input, "`(.+?)`", "<code>$1</code>");
            return input;
        }
    }
}