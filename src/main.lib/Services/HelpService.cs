using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins;
using PKISharp.WACS.Plugins.Base.Capabilities;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PKISharp.WACS.Services
{
    internal partial class HelpService(ILogService log, IPluginService plugins, ArgumentsParser parser)
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
        /// Show command line arguments for the help function
        /// </summary>
        internal void ShowArguments()
        {
            Console.WriteLine();
            var providers = parser.Providers;

            var providerPlugins = providers.Select(provider => new { 
                provider, 
                plugin = Plugin(provider) 
            }).ToList();

            var providerPluginGroups = providerPlugins.Select(p => new {
                p.provider,
                p.plugin,
                name = p.plugin?.Name ?? p.provider.Name,
                order = getOrder(p.plugin),
                group = getGroup(p.plugin) ?? p.provider.Group
            });

            int getOrder(Plugin? plugin)
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

            string? getGroup(Plugin? plugin)
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

            var orderedGroups = providerPluginGroups.
                GroupBy(p => p.group).
                OrderBy(g => g.Min(x => x.order));

            foreach (var ppgs in orderedGroups)
            {
                if (!string.IsNullOrEmpty(ppgs.Key))
                {
                    Console.WriteLine($" ---------------------");
                    Console.WriteLine($" {ppgs.Key}");
                    Console.WriteLine($" ---------------------");
                    Console.WriteLine();
                }

                foreach (var ppg in ppgs)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"    {ppg.name}");
                    Console.ResetColor();
                    if (ppg.plugin != null)
                    {
                        Console.WriteLine($"    [--{ppg.plugin.Step.ToString().ToLower()} {ppg.plugin.Name.ToLower()}]");
                    }
                    Console.WriteLine();
                    foreach (var x in ppg.provider.Configuration.Where(x => !x.Obsolete))
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.Write($"     --{x.ArgumentName}");
                        Console.WriteLine();
                        Console.ResetColor();
                        var step = 60;
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

        internal static string? EscapeConsole(string? input)
        {
            if (input == null) { return null; };
            input = BacktickRegex().Replace(input, "$1");
            return input;
        }

        internal static string EscapeYaml(string input)
        {
            input = input.Replace("\"", "\\\""); // Escape quote
            input = input.Replace("--", "‑‑"); // Regular hyphen to non-breaking
            input = BacktickRegex().Replace(input, "<code>$1</code>");
            input = UrlRegex().Replace(input, "<a href=\"$1\">$1</a>");
            return input;
        }

        [GeneratedRegex("`(.+?)`")]
        private static partial Regex BacktickRegex();

        [GeneratedRegex("https://simple-acme.com([^ ]+?)")]
        private static partial Regex UrlRegex();
    }
}
