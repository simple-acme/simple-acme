using ACMESharp;
using Microsoft.Extensions.Configuration;
using PKISharp.WACS.Configuration.Settings;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Settings.Configuration;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;

namespace PKISharp.WACS.Services
{
    public class LogService : ILogService, IAcmeLogger
    {
        private readonly Logger? _screenLogger;
        private readonly Logger? _debugScreenLogger;
        private Logger? _eventLogger;
        private Logger? _diskLogger;

        private readonly Logger? _notificationLogger;
        private readonly LoggingLevelSwitch _levelSwitch;

        public bool Dirty { get; set; }
        private string ConfigurationPath { get; }

        // Logging for notification emails
        private readonly List<MemoryEntry> _lines = [];
        public IEnumerable<MemoryEntry> Lines => _lines.AsEnumerable();
        public void Reset() => _lines.Clear();

        // Logging before the disk/event log configuration is available
        private List<LogEntry>? _logs = [];

        public LogService(bool verbose, bool config)
        {
            // Custom configuration support
            ConfigurationPath = Path.Combine(VersionService.BasePath, "serilog.json");
#if DEBUG
            var initialLevel = LogEventLevel.Debug;
#else
            var initialLevel = LogEventLevel.Information;
#endif
            if (verbose)
            {
                initialLevel = LogEventLevel.Verbose;
            }
            if (config)
            {
                initialLevel = LogEventLevel.Fatal;
            }
            _levelSwitch = new LoggingLevelSwitch(initialMinimumLevel: initialLevel);
            try
            {
                var theme = 
                    OperatingSystem.IsWindowsVersionAtLeast(10) || !OperatingSystem.IsWindows() ?
                    (ConsoleTheme)AnsiConsoleTheme.Code : 
                    SystemConsoleTheme.Literate;

                _screenLogger = new LoggerConfiguration()
                    .MinimumLevel.ControlledBy(_levelSwitch)
                    .Enrich.FromLogContext()
                    .Filter.ByIncludingOnly(x => { Dirty = true; return true; })
                    .WriteTo.Console(
                        outputTemplate: " {Message:l}{NewLine}", 
                        theme: theme)
                    .CreateLogger();
                _debugScreenLogger = new LoggerConfiguration()
                    .MinimumLevel.ControlledBy(_levelSwitch)
                    .Enrich.FromLogContext()
                    .Filter.ByIncludingOnly(x => { Dirty = true; return true; })
                    .WriteTo.Console(
                        outputTemplate: " [{Level:u4}] {Message:l}{NewLine}{Exception}", 
                        theme: theme)
                    .CreateLogger();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($" Error creating screen logger: {ex.Message} - {ex.StackTrace}");
                Console.ResetColor();
                Console.WriteLine();
                Environment.Exit(ex.HResult);
            }

            _notificationLogger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(_levelSwitch)
                .Enrich.FromLogContext()
                .WriteTo.Memory(_lines)
                .CreateLogger();

            Debug("Logging at level {initialLevel}", initialLevel);
        }

        /// <summary>
        /// The disk and event loggers are created after construction 
        /// of the LogService when the ClientSettings are loaded. 
        /// Before that all happens we can only log to the screen.
        /// </summary>
        /// <param name="settings"></param>
        public void ApplyClientSettings(ClientSettings settings)
        {
            CreateDiskLogger(settings.LogPath ?? settings.ConfigurationPath);
            if (OperatingSystem.IsWindows())
            {
                CreateEventLogger(settings.ClientName ?? "simple-acme");
            }
            if (_logs != null)
            {
                var logs = _logs.AsReadOnly();
                _logs = null;
                Information(LogType.Disk, "---------------------------------------------");
                Information(LogType.Disk, "---- LOG STARTS -----------------------------");
                Information(LogType.Disk, "---------------------------------------------");
                foreach (var log in logs)
                {
                    Write(log);
                }
            }     
        }

        /// <summary>
        /// Set up the disk logger
        /// </summary>
        /// <param name="logPath"></param>
        private void CreateDiskLogger(string logPath)
        {
            try
            {
                var defaultPath = Path.Combine(logPath.TrimEnd('\\', '/'), "log-.txt");
                var defaultRollingInterval = RollingInterval.Day;
                var defaultRetainedFileCountLimit = 120;
                var fileConfig = new ConfigurationBuilder()
                   .AddJsonFile(ConfigurationPath, true, true)
                   .Build();

                foreach (var writeTo in fileConfig.GetSection("disk:WriteTo").GetChildren())
                {
                    if (writeTo.GetValue<string>("Name") == "File")
                    {
                        var pathSection = writeTo.GetSection("Args:path");
                        if (string.IsNullOrEmpty(pathSection.Value))
                        {
                            pathSection.Value = defaultPath;
                        }
                        var retainedFileCountLimit = writeTo.GetSection("Args:retainedFileCountLimit");
                        if (string.IsNullOrEmpty(retainedFileCountLimit.Value))
                        {
                            retainedFileCountLimit.Value = defaultRetainedFileCountLimit.ToString();
                        }
                        var rollingInterval = writeTo.GetSection("Args:rollingInterval");
                        if (string.IsNullOrEmpty(rollingInterval.Value))
                        {
                            rollingInterval.Value = ((int)defaultRollingInterval).ToString();
                        }
                    }
                }

                _diskLogger = new LoggerConfiguration()
                     .MinimumLevel.Verbose()
                     .Enrich.FromLogContext()
                     .Enrich.WithProperty("ProcessId", Environment.ProcessId)
                     .WriteTo.File(
                         defaultPath,
                         rollingInterval: defaultRollingInterval,
                         retainedFileCountLimit: defaultRetainedFileCountLimit,
                         outputTemplate: " {Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u4}] {Message:l}{NewLine} {Exception}")
                     .ReadFrom.Configuration(fileConfig, new ConfigurationReaderOptions(typeof(LogService).Assembly) { SectionName = "disk" })
                     .CreateLogger();
            }
            catch (Exception ex)
            {
                Warning(ex, "Error creating disk logger");
            }
        }

        /// <summary>
        /// Set up the Windows Event Viewer logger
        /// </summary>
        /// <param name="source"></param>
        [SupportedOSPlatform("windows")]
        private void CreateEventLogger(string source)
        {
            try
            {
                var _eventConfig = new ConfigurationBuilder()
                    .AddJsonFile(ConfigurationPath, true, true)
                    .Build();

                _eventLogger = new LoggerConfiguration()
                    .MinimumLevel.ControlledBy(_levelSwitch)
                    .Enrich.FromLogContext()
                    .WriteTo.EventLog(source, manageEventSource: true)
                    .ReadFrom.Configuration(_eventConfig, new ConfigurationReaderOptions(typeof(LogService).Assembly) { SectionName = "event" })
                    .CreateLogger();
            }
            catch (Exception ex)
            {
                Warning(ex, "Error creating event logger");
            }
        }

        public void Verbose(string message, params object?[] items) => 
            Write(new LogEntry() { 
                type = LogType.Screen | LogType.Disk, 
                level = LogEventLevel.Verbose, 
                message = message, 
                items = items 
            });

        public void Debug(string message, params object?[] items) =>
            Write(new LogEntry() {
                type = LogType.Screen | LogType.Disk,
                level = LogEventLevel.Debug,
                message = message,
                items = items
            });

        public void Information(string message, params object?[] items) =>
            Information(LogType.Screen | LogType.Disk, message, items);

        public void Information(LogType logType, string message, params object?[] items) =>
            Write(new LogEntry() {
                type = logType,
                level = LogEventLevel.Information,
                message = message,
                items = items
            });

        public void Warning(string message, params object?[] items) =>
            Warning(null, message, items);

        public void Warning(Exception? ex, string message, params object?[] items) => 
            Write(new LogEntry(){
                type = LogType.All,
                level = LogEventLevel.Warning,
                message = message,
                items = items,
                ex = ex
            });

        public void Error(string message, params object?[] items) =>
            Error(null, message, items);

        public void Error(Exception? ex, string message, params object?[] items) => 
            Write(new LogEntry() {
                type = LogType.All,
                level = LogEventLevel.Error,
                message = message,
                items = items,
                ex = ex
            });


        /// <summary>
        /// Handle writes to different syncs
        /// </summary>
        /// <param name="entry"></param>
        private void Write(LogEntry entry)
        {
            if (entry.type.HasFlag(LogType.Screen))
            {
                if (_screenLogger != null && _levelSwitch.MinimumLevel >= LogEventLevel.Information)
                {
                    _screenLogger.Write(entry.level, entry.ex, entry.message, entry.items);
                }
                else
                {
                    _debugScreenLogger?.Write(entry.level, entry.ex, entry.message, entry.items);
                }
                _notificationLogger?.Write(entry.level, entry.ex, entry.message, entry.items);
            }
            if (_eventLogger != null && entry.type.HasFlag(LogType.Event))
            {
                _eventLogger.Write(entry.level, entry.ex, entry.message, entry.items);
            } 
            if (_diskLogger != null && entry.type.HasFlag(LogType.Disk))
            {
                _diskLogger.Write(entry.level, entry.ex, entry.message, entry.items);
            }

            // Save for relogging after disk/event log become available, but do not print to screen again
            _logs?.Add(new LogEntry() { 
                type = entry.type ^ LogType.Screen, 
                level = entry.level, 
                ex = entry.ex, 
                message = entry.message, 
                items = entry.items 
            });
        }

        /// <summary>
        /// Single log entry
        /// </summary>
        private record LogEntry
        {
            public LogType type;
            public LogEventLevel level;
            public Exception? ex;
            public required string message;
            public required object?[] items;
        }

    }
}
