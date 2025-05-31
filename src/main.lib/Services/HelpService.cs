using Autofac;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Settings;
using PKISharp.WACS.Plugins;
using PKISharp.WACS.Plugins.Base.Capabilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace PKISharp.WACS.Services
{
    internal partial class HelpService(
        ILogService log,
        IPluginService plugins,
        ISettings settings, 
        ArgumentsParser parser)
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
        internal IEnumerable<Plugin> DefaultTypeValidationPlugins() {
            var defaultType = settings.Validation.DefaultValidationMode;
            return plugins.
                GetPlugins(Steps.Validation).
                    Where(p => !p.Hidden).
                    Where(s => string.Equals(GetValidationType(s), defaultType, StringComparison.OrdinalIgnoreCase)).
                    ToList();
        }

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
                Condition = GetCondition(defaultTypePlugins, p.plugin)
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
                        if (ppg.Condition != null)
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
                    if (ppg.Condition != null)
                    {
                        Console.WriteLine($" [{ppg.Condition}]");
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
        /// Condition
        /// </summary>
        /// <param name="defaultTypePlugins"></param>
        /// <param name="plugin"></param>
        /// <returns></returns>
        private static string? GetCondition(IEnumerable<Plugin> defaultTypePlugins, Plugin? plugin)
        {
            if (plugin is null)
            {
                return null;
            }
            var duplicate = !defaultTypePlugins.Contains(plugin) && defaultTypePlugins.Any(n => string.Equals(n.Trigger, plugin?.Trigger));
            if (duplicate && plugin.Step == Steps.Validation)
            {
                var mode = Constants.Http01ChallengeType;
                if (plugin.Capability.IsAssignableTo(typeof(DnsValidationCapability)))
                {
                    mode = Constants.Dns01ChallengeType;
                }
                if (plugin.Capability.IsAssignableTo(typeof(TlsValidationCapability)))
                {
                    mode = Constants.TlsAlpn01ChallengeType;
                }
                return $"--validationmode {mode} --validation {plugin.Trigger.ToLower()}";
            }
            else
            {
                return $"--{plugin.Step.ToString().ToLower()} {plugin.Trigger.ToLower()}";
            }
        }

        /// <summary>
        /// Determine validation plugin subtype
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns></returns>
        private static string? GetValidationType(Plugin plugin)
        {
            if (plugin.Capability.IsAssignableTo(typeof(HttpValidationCapability)))
            {
                return Constants.Http01ChallengeType;
            }
            if (plugin.Capability.IsAssignableTo(typeof(DnsValidationCapability)))
            {
                return Constants.Dns01ChallengeType;
            }
            if (plugin.Capability.IsAssignableTo(typeof(TlsValidationCapability)))
            {
                return Constants.TlsAlpn01ChallengeType;
            }
            return null;
        }

        /// <summary>
        /// Determine validation plugin subtype
        /// </summary>
        /// <param name="plugin"></param>
        /// <returns></returns>
        private static string GetPluginType(Plugin plugin)
        {
            var ret = plugin.Step.ToString().ToLower();
            if (plugin.Step == Steps.Validation)
            {
                var validationType = GetValidationType(plugin);
                if (validationType != null)
                {
                    ret += "." + validationType.Replace("-01", "");
                }
            }
            return ret;
        }

        /// <summary>
        /// Generate arguments YAML for documentation website
        /// </summary>
        internal void GenerateArgumentsYaml()
        {
            var groups = GetDocumentations();
            var x = new StringBuilder();
            foreach (var providerGroup in groups)
            {
                foreach (var provider in providerGroup)
                {
                    x.AppendLine($"-");
                    x.AppendLine($" name: {provider.Name}");
                    if (provider.Plugin != null)
                    {
                        x.AppendLine($" pluginid: \"{provider.Plugin.Id.ToString().ToLower()}\"");
                        x.AppendLine($" plugintype: \"{GetPluginType(provider.Plugin)}\"");
                    }
                    if (provider.Condition != null)
                    {
                        x.AppendLine($" condition: \"{provider.Condition}\"");
                    }
                    x.AppendLine($" arguments:");
                    foreach (var c in provider.Provider.Configuration.Where(x => !x.Obsolete))
                    {
                        x.AppendLine($"  -");
                        x.AppendLine($"   name: {c.ArgumentName}");
                        if (c.Description != null)
                        {
                            x.AppendLine($"   description: \"{EscapeYaml(c.Description)}\"");
                        }
                        if (c.Default != null)
                        {
                            x.AppendLine($"   default: \"{EscapeYaml(c.Default)}\"");
                        }
                        if (c.Secret == true)
                        {
                            x.AppendLine($"   secret: true");
                        }
                    }
                    x.AppendLine();
                }
            }
            File.WriteAllText("arguments.yml", x.ToString());
            log.Debug("YAML written to {0}", new FileInfo("arguments.yml").FullName);
        }

        /// <summary>
        /// Generate plugins.yml for documentation website
        /// </summary>
        internal void GeneratePluginsYaml()
        {
            var x = new StringBuilder();
            foreach (var plugin in plugins.GetPlugins().Where(p => !p.Hidden))
            {
                x.AppendLine($"-");
                x.AppendLine($" name: \"{EscapeYaml(plugin.Name)}\"");
                x.AppendLine($" id: {plugin.Id.ToString().ToLower()}");
                x.AppendLine($" trigger: {plugin.Trigger.ToLower()}");
                if (plugin.External == true)
                {
                    x.AppendLine($" external: true");
                }
                if (plugin.Schema == true)
                {
                    x.AppendLine($" schema: true");
                }
                if (plugin.Provider != null)
                {
                    x.AppendLine($" provider: {plugin.Provider}");
                }
                if (plugin.Page != null)
                {
                    x.AppendLine($" page: {plugin.Page}");
                }
                if (plugin.Download != null)
                {
                    x.AppendLine($" download: {plugin.Download}");
                }
                x.AppendLine($" description: \"{EscapeYaml(plugin.Description)}\"");
                x.AppendLine($" type: {GetPluginType(plugin)}");
                x.AppendLine();
            }
            File.WriteAllText("plugins.yml", x.ToString());
            log.Debug("YAML written to {0}", new FileInfo("plugins.yml").FullName);
        }

        /// <summary>
        /// Generate settings.yml for documentation website
        /// </summary>
        internal void GenerateSettingsYaml()
        {
            var metaBuilder = new StringBuilder();
            GenerateSettingsYamlForType(typeof(Settings), 0, metaBuilder);
            File.WriteAllText("settings.yml", metaBuilder.ToString());
            log.Debug("YAML written to {0}", new FileInfo("settings.yml").FullName);
        }

        internal void GenerateSettingsYamlForType(Type t, int level, StringBuilder x)
        {
            foreach (var member in t.GetMembers(BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Instance).OfType<PropertyInfo>())
            {
                var meta = member.GetCustomAttribute<SettingsValueAttribute>();
                if (meta?.Hidden ?? false)
                {
                    continue;
                }
                x.AppendJoin("", Enumerable.Repeat("  ", level));
                x.AppendLine($"{member.Name}:");
                if (member.PropertyType.IsInNamespace("PKISharp"))
                {
                    GenerateSettingsYamlForType(member.PropertyType, level + 1, x);
                } 
                else
                {
                    var type = member.PropertyType;
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        type = type.GetGenericArguments().First();
                    }
                    var showType = "";
                    var subType = meta?.SubType;
                    if (type == typeof(string))
                    {
                        showType = "string";
                    }
                    else if (type == typeof(bool))
                    {
                        showType = "boolean";
                    }
                    else if (type == typeof(int))
                    {
                        showType = "number";
                    }
                    else if (type == typeof(Uri))
                    {
                        showType = "string";
                        subType = "uri";
                    }
                    else if (type == typeof(List<string>))
                    {
                        showType = "string[]";
                    }
                    else if (type == typeof(TimeSpan))
                    {
                        showType = "string";
                        subType = "time";
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                    x.AppendJoin("", Enumerable.Repeat("  ", level + 1));
                    x.AppendLine($"type: \"{showType}\"");
                    if (!string.IsNullOrWhiteSpace(subType))
                    {
                        x.AppendJoin("", Enumerable.Repeat("  ", level + 1));
                        x.AppendLine($"subtype: \"{subType}\"");
                    }
                    if (!string.IsNullOrWhiteSpace(meta?.Default))
                    {
                        x.AppendJoin("", Enumerable.Repeat("  ", level + 1));
                        x.AppendLine($"default: \"{EscapeYaml(meta.Default)}\"");
                    }
                    if (!string.IsNullOrWhiteSpace(meta?.DefaultExtra))
                    {
                        x.AppendJoin("", Enumerable.Repeat("  ", level + 1));
                        x.AppendLine($"defaultExtra: \"{EscapeYaml(meta.DefaultExtra)}\"");
                    }
                    if (!string.IsNullOrWhiteSpace(meta?.NullBehaviour))
                    {
                        RenderMultiline(level, x, "nullBehaviour", meta.NullBehaviour);
                    }
                    if (!string.IsNullOrWhiteSpace(meta?.Description))
                    {
                        RenderMultiline(level, x, "description", meta.Description);
                    }
                    if (!string.IsNullOrWhiteSpace(meta?.Tip))
                    {
                        RenderMultiline(level, x, "tip", meta.Tip);
                    }
                    if (!string.IsNullOrWhiteSpace(meta?.Warning))
                    {
                        RenderMultiline(level, x, "warning", meta.Warning);
                    }
                    x.AppendLine();
                }
            }
        }

        /// <summary>
        /// Render a multiline string in YAML format
        /// </summary>
        /// <param name="level"></param>
        /// <param name="x"></param>
        /// <param name="label"></param>
        /// <param name="input"></param>
        private static void RenderMultiline(int level, StringBuilder x, string label, string input)
        {
            x.AppendJoin("", Enumerable.Repeat("  ", level + 1));
            x.Append($"{label}:");
            var parts = input.Split('\n').ToList();
            x.AppendLine();
            x.AppendJoin("", Enumerable.Repeat("  ", level + 2));
            x.Append($"\"{EscapeYaml(parts[0])}");
            if (parts.Count == 1)
            {
                x.AppendLine("\"");
            }
            else
            {
                x.AppendLine();
                x.AppendLine();
                foreach (var line in parts.Skip(1))
                {
                    x.AppendJoin("", Enumerable.Repeat("  ", level + 2));
                    x.Append(EscapeYaml(line));
                    if (parts.IndexOf(line) == parts.Count - 1)
                    {
                        x.AppendLine("\"");
                        x.AppendLine();
                    }
                    else
                    {
                        x.AppendLine();
                    }
                }
            }
        }

        /// <summary>
        /// Generate settings.yml for documentation website
        /// </summary>
        internal void GenerateSettingsYaml2()
        {
            var metaBuilder = new StringBuilder();
            foreach (var member in typeof(Settings).GetMembers(BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Instance).OfType<PropertyInfo>())
            {
                var meta = member.GetCustomAttribute<SettingsValueAttribute>();
                if (meta?.Hidden ?? false)
                {
                    continue;
                }
                metaBuilder.AppendLine($"{member.Name.ToLower()}:");
                GenerateSettingsYamlForType2(member.PropertyType, member.Name, metaBuilder);
            }
            File.WriteAllText("settings2.yml", metaBuilder.ToString());
            log.Debug("YAML written to {0}", new FileInfo("settings2.yml").FullName);
        }

        internal void GenerateSettingsYamlForType2(Type t, string prefix, StringBuilder x)
        {
            foreach (var member in t.GetMembers(BindingFlags.Public | BindingFlags.GetProperty | BindingFlags.Instance).OfType<PropertyInfo>())
            {
                var meta = member.GetCustomAttribute<SettingsValueAttribute>();
                if (meta?.Hidden ?? false)
                {
                    continue;
                }
                if (member.PropertyType.IsInNamespace("PKISharp"))
                {
                    if (meta?.Split == true)
                    {
                        x.AppendLine($"{member.Name.ToLower()}:");
                    }
                    GenerateSettingsYamlForType2(member.PropertyType, $"{prefix}.{member.Name}", x);
                } 
                else
                {
                    x.AppendLine($"  - {prefix}.{member.Name}");
                }
            }
        }

        /// <summary>
        /// Process compiled string for console display
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static string? EscapeConsole(string? input)
        {
            if (input == null) { return null; };
            var replace = "$1";
            if (InputService.SupportsVT100())
            {
                replace = $"{InputService.Green}{replace}{InputService.Reset}";
            }
            input = BacktickRegex().Replace(input, replace);
            return input;
        }

        /// <summary>
        /// Process compiled string for YAML documentation
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        internal static string EscapeYaml(string input)
        {
            input = input.Replace("\\", "\\\\"); // Escape backslash
            input = input.Replace("\"", "\\\""); // Escape quote
            input = input.Replace("--", "‑‑"); // Regular hyphen to non-breaking
            input = BacktickRegex().Replace(input, "<code>$1</code>");
            input = UrlRegex().Replace(input, "<a href='$1'>$1</a>");
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
            internal required string? Condition;
        }

        [GeneratedRegex("`(.+?)`")]
        private static partial Regex BacktickRegex();

        [GeneratedRegex("https:\\/\\/simple-acme\\.com([^ ]+)")]
        private static partial Regex UrlRegex();
    }
}
