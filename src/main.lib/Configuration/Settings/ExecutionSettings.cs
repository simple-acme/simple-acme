using PKISharp.WACS.Configuration.Settings.UI;
using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings
{
    /// <summary>
    /// Settings regarding the execution of the renewal
    /// </summary>
    public interface IExecutionSettings
    {
        /// <summary>
        /// Default script to run before executing a renewal
        /// </summary>
        string? DefaultPostExecutionScript { get; }

        /// <summary>
        /// Default script to run after execution a renewal
        /// </summary>
        string? DefaultPreExecutionScript { get; }
    }

    internal class InheritExecutionSettings(params IEnumerable<ExecutionSettings?> chain) : InheritSettings<ExecutionSettings>(chain), IExecutionSettings
    {
        public string? DefaultPostExecutionScript => Get(x => x.DefaultPostExecutionScript);
        public string? DefaultPreExecutionScript => Get(x => x.DefaultPreExecutionScript);
    }

    internal class ExecutionSettings
    {
        public string? DefaultPreExecutionScript { get; set; }
        public string? DefaultPostExecutionScript { get; set; }
    }
}