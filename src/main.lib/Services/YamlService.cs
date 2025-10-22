using Autofac;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Settings;
using PKISharp.WACS.Plugins;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace PKISharp.WACS.Services
{
    internal partial class YamlService(
        ILogService log,
        IPluginService plugins,
        ISettings settings,
        ArgumentsParser parser) : HelpService(plugins, settings, parser)
    {
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
            foreach (var plugin in Plugins.GetPlugins().Where(p => !p.Hidden))
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
                x.AppendLine($" options:");
                GenerateTypeYaml(plugin.Options, 1, x);
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
            GenerateTypeYaml(typeof(Settings), 0, metaBuilder);
            File.WriteAllText("settings.yml", metaBuilder.ToString());
            log.Debug("YAML written to {0}", new FileInfo("settings.yml").FullName);
        }

        /// <summary>
        /// YAML metadata for a specific type
        /// </summary>
        /// <param name="t"></param>
        /// <param name="level"></param>
        /// <param name="x"></param>
        /// <exception cref="NotImplementedException"></exception>
        internal static void GenerateTypeYaml(Type t, int level, StringBuilder x)
        {
            IEnumerable<PropertyInfo> properties = [];
#if PLUGGABLE
            properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
#endif
            foreach (var member in properties)
            {
                var meta = member.GetCustomAttribute<SettingsValueAttribute>();
                if (meta?.Hidden ?? false)
                {
                    continue;
                }
                x.AppendJoin("", Enumerable.Repeat("  ", level));
                x.AppendLine($"{member.Name}:");
                var type = member.PropertyType;
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    type = type.GetGenericArguments().First();
                }
                if (type.IsInNamespace("PKISharp") && member.PropertyType != typeof(ProtectedString))
                {
                    GenerateTypeYaml(type, level + 1, x);
                }
                else
                {
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
                    else if (type == typeof(int) || type == typeof(long))
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
                    else if (type == typeof(List<int>) || type == typeof(List<long>))
                    {
                        showType = "number[]";
                    }
                    else if (type == typeof(TimeSpan))
                    {
                        showType = "string";
                        subType = "time";
                    }
                    else if (type == typeof(ProtectedString))
                    {
                        showType = "string";
                        subType = "secret";
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
        /// Generate settings2.yml for documentation website
        /// </summary>
        internal void GenerateSettingsYaml2()
        {
            var metaBuilder = new StringBuilder();
            foreach (var member in typeof(Settings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
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

        internal static void GenerateSettingsYamlForType2(Type t, string prefix, StringBuilder x)
        {
            IEnumerable<PropertyInfo> properties = [];
#if PLUGGABLE
            properties = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
#endif
            foreach (var member in properties)
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

        [GeneratedRegex("https:\\/\\/simple-acme\\.com([^ ]+)")]
        private static partial Regex UrlRegex();
    }
}
