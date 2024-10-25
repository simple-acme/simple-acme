using PKISharp.WACS.Services;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;

namespace PKISharp.WACS.Services
{
    public class MemoryEntry(LogEventLevel level, string message)
    {
        public LogEventLevel Level { get; set; } = level;
        public string Message { get; set; } = message;
    }

    internal class MemorySink(List<MemoryEntry> list, IFormatProvider? formatProvider = null) : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => list.Add(new MemoryEntry(logEvent.Level, logEvent.RenderMessage(formatProvider)));
    }
}

namespace Serilog
{
    /// <summary>
    /// Adds the WriteTo.Memory() extension method to <see cref="LoggerConfiguration"/>.
    /// </summary>
    public static class LoggerConfigurationStackifyExtensions
    {
        public static LoggerConfiguration Memory(this LoggerSinkConfiguration loggerConfiguration, List<MemoryEntry> target, IFormatProvider? formatProvider = null) => loggerConfiguration.Sink(new MemorySink(target, formatProvider));
    }
}
