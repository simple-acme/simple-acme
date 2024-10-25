using Autofac.Core;
using System;
using System.Collections.Generic;

namespace PKISharp.WACS.Services
{
    /// <summary>
    /// Handles exception logging and process exit code
    /// </summary>
    /// <param name="log">
    /// Logging
    /// </param>
    internal class ExceptionHandler(ILogService log)
    {

        /// <summary>
        /// Code that we are going to exit the process with
        /// </summary>
        public int ExitCode { get; private set; } = 0;

        /// <summary>
        /// Handle exceptions by logging them and setting negative exit code
        /// </summary>
        /// <param name="innerex"></param>
        public string? HandleException(Exception? original = null, string? message = null)
        {
            var outMessage = message;
            var exceptionStack = new List<Exception>();
            if (original != null)
            {
                exceptionStack.Add(original);
                while (original.InnerException != null)
                {
                    original = original.InnerException;
                    exceptionStack.Add(original);
                }
                var innerMost = exceptionStack.Count - 1;
                for (var i = innerMost; i >= 0; i--)
                {
                    var currentException = exceptionStack[i];
                    if (i == innerMost)
                    {
                        outMessage = currentException.Message;
                        // InnerMost exception is logged with Error priority
                        if (!string.IsNullOrEmpty(message))
                        {
                            log.Error($"({{type}}) {message}: {{message}}", currentException.GetType().Name, currentException.Message);
                        }
                        else
                        {
                            log.Error("({type}): {message}", currentException.GetType().Name, currentException.Message);
                        }
                        log.Debug("Exception details: {@ex}", currentException);
                        ExitCode = currentException.HResult;
                    }
                    else if (
                        currentException is not DependencyResolutionException &&
                        currentException is not AggregateException)
                    {
                        // Outer exceptions up to the point of Autofac logged with error priority
                        log.Error("Wrapped in {type}: {message}", currentException.GetType().Name, currentException.Message);
                    }
                    else
                    {
                        // Autofac and Async exceptions only logged in debug/verbose mode
                        log.Debug("Wrapped in {type}: {message}", currentException.GetType().Name, currentException.Message);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(message))
            {
                log.Error(message);
            }
            ExitCode = -1;
            return outMessage;
        }

        /// <summary>
        /// Restore error
        /// </summary>
        internal void ClearError() => ExitCode = 0;
    }
}
