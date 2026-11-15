using Serilog.Events;

namespace PKISharp.WACS.Services
{
    public class MemoryEntry(LogEventLevel level, string message)
    {
        public LogEventLevel Level { get; set; } = level;
        public string Message { get; set; } = message;
    }
}