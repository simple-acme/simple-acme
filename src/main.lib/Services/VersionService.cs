using PKISharp.WACS.Plugins.Interfaces;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PKISharp.WACS.Services
{
    public class VersionService
    {
        private static Lazy<string> BasePath { get; } = new Lazy<string>(() =>
        {
             if (ExeFileInfo != null && ExeFileInfo.Value != null && ExeFileInfo.Value.Directory != null)
             {
                 return ExeFileInfo.Value.Directory.FullName + Path.DirectorySeparatorChar;
             }
             return string.Empty;
        });

        private static Lazy<FileInfo?> ProcessInfo { get; } = new Lazy<FileInfo?>(() =>
        {
            var module = Process.GetCurrentProcess().MainModule;
            if (module == null)
            {
                return null;
            }
            return new FileInfo(module.FileName);
        });

        private static Lazy<FileInfo?> ExeFileInfo { get; } = new Lazy<FileInfo?>(() =>
        {
            var cmd = Environment.GetCommandLineArgs().First();
            var fi = new FileInfo(cmd);
            if (!fi.Exists)
            {
                return null;
            }
            if (fi.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                try
                {
                    if (fi.ResolveLinkTarget(true) is FileInfo target)
                    {
                        return target;
                    }
                }
                catch
                {
                    // Ignore exceptions from ResolveLinkTarget,
                    // just return the original file info
                }
            }
            return fi;
        });

        internal static Lazy<State> Valid { get; } = new Lazy<State>(() =>
        {
            var exeFileInfo = ExeFileInfo.Value;
            if (exeFileInfo == null || exeFileInfo.Directory == null)
            {
                return State.DisabledState("Unable to determine main module filename.");
            }

            // Check for running as local .NET tool
            if (exeFileInfo.Name == "dotnet.exe")
            {
                return State.DisabledState("Running as a local dotnet tool is not supported. Please install using the --global option.");
            }
            return State.EnabledState();
        });

        internal static bool DotNetTool => ExeFileInfo.Value?.Name == "wacs.dll" && !Debug;
        internal static string SettingsPath => DotNetTool && ProcessInfo.Value != null && ProcessInfo.Value.Directory != null ? 
            Path.Combine(ProcessInfo.Value.Directory.FullName, ".store", "simple-acme") : 
            BasePath.Value;
        internal static string PluginPath => BasePath.Value;
        internal static string ExePath => ExeFileInfo.Value?.FullName ?? "unknown";
        internal static string ResourcePath => DotNetTool ? AppContext.BaseDirectory : BasePath.Value;
        internal static string Bitness => Environment.Is64BitProcess ? "64-bit" : "32-bit";
        internal static bool Pluggable =>
#if PLUGGABLE
                true;
#else
                false;
#endif
        internal static bool Debug =>
#if DEBUG
                true;
#else
                false;
#endif

        internal static string BuildType 
        { 
            get
            {
                var build = $"{(Debug ? "debug" : "release")}, " +
                    $"{(Pluggable ? "pluggable" : "trimmed")}, " +
                    $"{(DotNetTool ? "dotnet" : "standalone")}";
                return build;
            }
        }

        public static Version SoftwareVersion => 
            Assembly.GetEntryAssembly()?.GetName().Version ?? 
            new Version("2.2.0.0");
    }
}
