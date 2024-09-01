using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins;
using PKISharp.WACS.Plugins.Base.Capabilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PKISharp.WACS.Services
{
    internal partial class HelpService(ILogService log, IPluginService plugins, ISettingsService settings, ArgumentsParser parser)
    {
        /// <summary>
        /// Map arguments to plugins
        /// </summary>
        /// <returns></returns>
        internal Plugin? Plugin(IArgumentsProvider provider) => plugins.
            GetPlugins().
            Where(p => !p.Hidden).
            Where(p => p.Arguments == provider.GetType().GetGenericArguments().First()).
            FirstOrDefault();

        /// <summary>
        /// Map arguments to plugins
        /// </summary>
        /// <returns></returns>
        internal IEnumerable<Plugin> DefaultTypeValidationPlugins() => plugins.
            GetPlugins(Steps.Validation).
                Where(p => !p.Hidden).
                Where(s =>
                {
                    return (settings.Validation.DefaultValidationMode ?? Constants.DefaultChallengeType).ToLower() switch
                    {
                        Constants.Http01ChallengeType => s.Capability.IsAssignableTo(typeof(HttpValidationCapability)),
                        Constants.Dns01ChallengeType => s.Capability.IsAssignableTo(typeof(DnsValidationCapability)),
                        Constants.TlsAlpn01ChallengeType => s.Capability.IsAssignableTo(typeof(TlsValidationCapability)),
                        _ => false,
                    };
                }).
                ToList();

        internal IOrderedEnumerable<IGrouping<string, Documentation>> GetDocumentations()
        {
            var providers = parser.Providers;
            var providerPlugins = providers.Select(provider => new {
                provider,
                plugin = Plugin(provider)
            }).ToList();

            var defaultTypePlugins = DefaultTypeValidationPlugins();
            var providerPluginGroups = providerPlugins.Select(p => new Documentation()
            {
                Plugin = p.plugin,
                Provider = p.provider,
                Name = p.plugin?.Name ?? p.provider.Name,
                Order = GetOrder(p.plugin),
                Group = GetGroup(p.plugin) ?? p.provider.Group,
                Duplicate = !defaultTypePlugins.Contains(p.plugin) && defaultTypePlugins.Any(n => string.Equals(n.Trigger, p.plugin?.Trigger))
            });

            var orderedGroups = providerPluginGroups.
                GroupBy(p => p.Group).
                OrderBy(g => g.Min(x => x.Order));
            return orderedGroups;
        }

        /// <summary>
        /// Show command line arguments for the help function
        /// </summary>
        internal void ShowArguments()
        {
            var groups = GetDocumentations();
            Console.WriteLine();
            foreach (var ppgs in groups)
            {
                var label = ppgs.Key;
                if (string.IsNullOrEmpty(label))
                {
                    label = "Main";
                };
                Console.WriteLine($" ---------------------");
                Console.WriteLine($" {label}");
                Console.WriteLine($" ---------------------");
                Console.WriteLine("");

                foreach (var ppg in ppgs)
                {
                    if (ppg.Name != "Main")
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        if (ppg.Plugin != null)
                        {
                            Console.Write($" * {ppg.Name}");
                        }
                        else
                        {
                            Console.WriteLine($" * {ppg.Name}");
                            Console.WriteLine();
                        }
                        Console.ResetColor();
                    }
                    if (ppg.Plugin != null)
                    {
                        if (ppg.Duplicate && ppg.Plugin.Step == Steps.Validation)
                        {
                            var mode = Constants.Http01ChallengeType; 
                            if (ppg.Plugin.Capability.IsAssignableTo(typeof(DnsValidationCapability)))
                            {
                                mode = Constants.Dns01ChallengeType;
                            }
                            if (ppg.Plugin.Capability.IsAssignableTo(typeof(TlsValidationCapability)))
                            {
                                mode = Constants.TlsAlpn01ChallengeType;
                            }
                            Console.WriteLine($" [--validationmode {mode} --{ppg.Plugin.Step.ToString().ToLower()} {ppg.Plugin.Trigger.ToLower()}]");
                        } 
                        else
                        {
                            Console.WriteLine($" [--{ppg.Plugin.Step.ToString().ToLower()} {ppg.Plugin.Trigger.ToLower()}]");
                        }
                        Console.WriteLine();
                    }
                    foreach (var x in ppg.Provider.Configuration.Where(x => !x.Obsolete))
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"     --{x.ArgumentName}");
                        Console.WriteLine();
                        Console.ResetColor();
                        var step = 80;
                        var pos = 0;
                        var words = EscapeConsole(x.Description)?.Split(' ') ?? [];
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
                }
            }
        }

        /// <summary>
        /// Determine plugin group
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns></returns>
        private static string? GetGroup(Plugin? plugin)
        {
            if (plugin is null)
            {
                return null;
            }
            if (plugin.Step == Steps.Validation)
            {
                if (plugin.Capability.IsAssignableTo(typeof(DnsValidationCapability)))
                {
                    return "DNS validation";
                }
                if (plugin.Capability.IsAssignableTo(typeof(TlsValidationCapability)))
                {
                    return "TLS validation";
                }
                return "HTTP validation";
            }
            return plugin.Step.ToString();
        }

        /// <summary>
        /// Determine plugin order
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns></returns>
        private static int GetOrder(Plugin? plugin)
        {
            if (plugin is null)
            {
                return -1;
            }
            var offset = 0;
            if (plugin.Step == Steps.Validation)
            {
                if (plugin.Capability.IsAssignableTo(typeof(DnsValidationCapability)))
                {
                    offset = 1;
                }
                if (plugin.Capability.IsAssignableTo(typeof(TlsValidationCapability)))
                {
                    offset = 2;
                }
            }
            return (int)plugin.Step + offset;
        }

        /// <summary>
        /// Generate YAML for documentation website
        /// </summary>
        internal void ShowArgumentsYaml()
        {
            var x = new StringBuilder();
            foreach (var providerGroup in parser.Providers.GroupBy(p => p.Group).OrderBy(g => g.Key))
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
                    var plugin = Plugin(provider);
                    if (plugin != null)
                    {
                        x.AppendLine($"         plugin: {plugin.Id}");
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
            log.Debug("YAML written to {0}", new FileInfo("arguments.yaml").FullName);
        }

        /// <summary>
        /// Process compiled string for console display
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static string? EscapeConsole(string? input)
        {
            if (input == null) { return null; };
            input = BacktickRegex().Replace(input, "$1");
            return input;
        }

        /// <summary>
        /// Process compiled string for YAML documentation
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static string EscapeYaml(string input)
        {
            input = input.Replace("\"", "\\\""); // Escape quote
            input = input.Replace("--", "‑‑"); // Regular hyphen to non-breaking
            input = BacktickRegex().Replace(input, "<code>$1</code>");
            input = UrlRegex().Replace(input, "<a href=\"$1\">$1</a>");
            return input;
        }

        /// <summary>
        /// Helper record to group documentation groups
        /// </summary>
        internal record Documentation
        {
            internal required string Group;
            internal required int Order;
            internal required IArgumentsProvider Provider;
            internal Plugin? Plugin;
            internal required string Name;
            internal bool Duplicate = false;
        }

        [GeneratedRegex("`(.+?)`")]
        private static partial Regex BacktickRegex();

        [GeneratedRegex("https://simple-acme.com([^ ]+?)")]
        private static partial Regex UrlRegex();
    }
}
