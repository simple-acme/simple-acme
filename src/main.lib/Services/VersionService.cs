using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PKISharp.WACS.Services
{
    public class VersionService
    {
        internal static bool Init(ILogService log)
        {
            var exeFileInfo = ExeFileInfo;
            if (exeFileInfo == null || exeFileInfo.Directory == null)
            {
                log.Error("Unable to determine main module filename.");
                return false;
            }

            // Check for running as local .NET tool
            if (exeFileInfo.Name == "dotnet.exe")
            {
                log.Error("Running as a local dotnet tool is not supported. Please install using the --global option.");
                return false;
            }

            // Defaults
            ExePath = exeFileInfo.FullName;
            var path = exeFileInfo.Directory.FullName + Path.DirectorySeparatorChar;
            SettingsPath = path;
            PluginPath = path;
            ResourcePath = path;

            // Check for running as global .NET tool
            if (exeFileInfo.Name == "wacs.dll" && !Debug)
            {
                DotNetTool = true;
                var processInfo = new FileInfo(Process.GetCurrentProcess().MainModule?.FileName!);
                ExePath = processInfo.FullName;
                SettingsPath = Path.Combine(processInfo.Directory!.FullName, ".store", "simple-acme");
                PluginPath = exeFileInfo.DirectoryName!;
                ResourcePath = AppContext.BaseDirectory;
            }

            log.Verbose("ExePath: {ex}", ExePath);
            if (DotNetTool)
            {
                log.Verbose("ResourcePath: {ex}", ResourcePath);
                log.Verbose("PluginPath: {ex}", PluginPath);
                log.Verbose("SettingsPath: {ex}", SettingsPath);
            }
            Valid = true;
            return true;
        }

        internal static FileInfo? ExeFileInfo
        {
            get
            {
                var cmd = Environment.GetCommandLineArgs().First();
                var fi = new FileInfo(cmd);
                if (!fi.Exists)
                {
                    return null;
                }
                if (fi.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    if (fi.ResolveLinkTarget(true) is FileInfo target)
                    {
                        return target;
                    }
                }
                return fi;
            }
        }

        internal static bool Valid { get; private set; } = false;
        internal static bool DotNetTool { get; private set; } = false;
        internal static string SettingsPath { get; private set; } = string.Empty;
        internal static string PluginPath { get; private set; } = string.Empty;
        internal static string ExePath { get; private set; } = string.Empty;
        internal static string ResourcePath { get; private set; } = string.Empty;
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
