﻿using ACMESharp;
using PKISharp.WACS.Configuration.Settings.Types;
using PKISharp.WACS.Services;
using Serilog;
using Serilog.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace PKISharp.WACS.UnitTests.Mock.Services
{
    internal class LogService(bool throwErrors) : ILogService, IAcmeLogger
    {
        private readonly Logger _logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console(outputTemplate: " [{Level:u4}] {Message:l}{NewLine}{Exception}")
                .CreateLogger();

        public ConcurrentQueue<string> DebugMessages { get; } = new ConcurrentQueue<string>();
        public ConcurrentQueue<string> WarningMessages { get; } = new ConcurrentQueue<string>();
        public ConcurrentQueue<string> InfoMessages { get; } = new ConcurrentQueue<string>();
        public ConcurrentQueue<string> ErrorMessages { get; } = new ConcurrentQueue<string>();
        public ConcurrentQueue<string> VerboseMessages { get; } = new ConcurrentQueue<string>();

        public LogService() : this(false) {}

        public bool Dirty { get; set; }

        public IEnumerable<MemoryEntry> Lines => [];

        public void Debug(string message, params object?[] items)
        {
            DebugMessages.Enqueue(message);
            _logger.Debug(message, items);
        }
        public void Error(Exception? ex, string message, params object?[] items)
        {
            ErrorMessages.Enqueue(message);
            _logger.Error(ex, message, items);
            if (throwErrors && ex != null)
            {
                throw ex;
            }
        }
        public void Error(string message, params object?[] items)
        {
            ErrorMessages.Enqueue(message);
            _logger.Error(message, items);
            if (throwErrors)
            {
                throw new Exception(message);
            }
        }

        public void Information(LogType logType, string message, params object?[] items)
        {
            InfoMessages.Enqueue(message);
            _logger.Information(message, items);
        }

        public void Information(string message, params object?[] items) => Information(LogType.All, message, items);

        public void Verbose(string message, params object?[] items)
        {
            VerboseMessages.Enqueue(message);
            _logger.Verbose(message, items);
        }
        public void Verbose(LogType _, string message, params object?[] items)
        {
            VerboseMessages.Enqueue(message);
            _logger.Verbose(message, items);
        }
        public void Warning(string message, params object?[] items)
        {
            WarningMessages.Enqueue(message);
            _logger.Warning(message, items);
        }

        public void Reset() { }

        public void ApplyClientSettings(IClientSettings logPath) {}

        public void Warning(Exception? ex, string message, params object?[] items)
        {
            WarningMessages.Enqueue(message);
            _logger.Warning(ex, message, items);
        }
    }
}
