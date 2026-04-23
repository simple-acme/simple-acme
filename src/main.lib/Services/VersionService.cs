using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PKISharp.WACS.Services
{
    public class VersionService
    {
        /// <summary>
        /// Default client name if none can be determined from the settings
        /// </summary>
        internal const string DefaultClientName = "simple-acme";

        /// <summary>
        /// File where the resources are (like template settings)
        /// </summary>
        private static Lazy<string> BasePath { get; } = new Lazy<string>(() =>
        {
            var assemblyLocation =
                Assembly.GetEntryAssembly()?.Location ??
                Assembly.GetExecutingAssembly()?.Location;
            if (!string.IsNullOrWhiteSpace(assemblyLocation))
            {
                var assemblyFile = new FileInfo(assemblyLocation);
                if (assemblyFile.Exists)
                {
                    return assemblyFile.Directory?.FullName + Path.DirectorySeparatorChar ?? string.Empty;
                }
            }
            if (ExeFileInfo.Value != null)
            {
                var resolved = ResolveParsePoints(ExeFileInfo.Value);
                return resolved.Directory?.FullName + Path.DirectorySeparatorChar ?? string.Empty;
            }
            return string.Empty;
        });

        /// <summary>
        /// File the task scheduler should point to
        /// </summary>
        private static Lazy<FileInfo?> ExeFileInfo { get; } = new Lazy<FileInfo?>(() =>
        {
            var module = Process.GetCurrentProcess().MainModule;
            if (module == null)
            {
                return null;
            }
            return new FileInfo(module.FileName);
        });

        /// <summary>
        /// File that launched the process, to detect if we're running as a
        /// dotnet tool
        /// </summary>
        private static Lazy<FileInfo?> LaunchInfo { get; } = new Lazy<FileInfo?>(() =>
        {
            var cmd = Environment.GetCommandLineArgs().First();
            var fi = new FileInfo(cmd);
            if (!fi.Exists)
            {
                return null;
            }
            return fi;
        });

        /// <summary>
        /// Resolve parse points to get the actual file info, if possible. 
        /// This is important for scenarios where the executable is a symlink or junction, 
        /// such as when running as a global WinGet tool.
        /// </summary>
        /// <param name="fi"></param>
        /// <returns></returns>
        private static FileInfo ResolveParsePoints(FileInfo fi)
        {
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
        }

        internal static bool DotNetTool => LaunchInfo.Value?.Name == "wacs.dll" && !Debug;
        internal static string SettingsPath => DotNetTool ? Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), DefaultClientName) : BasePath.Value;
        internal static string ExePath => ExeFileInfo.Value?.FullName ?? string.Empty;
        internal static string ResourcePath => BasePath.Value;
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
