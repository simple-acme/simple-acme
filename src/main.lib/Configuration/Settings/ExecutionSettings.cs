namespace PKISharp.WACS.Configuration.Settings
{
    /// <summary>
    /// Settings regarding the execution of the renewal
    /// </summary>
    public class ExecutionSettings
    {
        /// <summary>
        /// Default script to run before executing a renewal
        /// </summary>
        public string? DefaultPreExecutionScript { get; set; }
        /// <summary>
        /// Default script to run after execution a renewal
        /// </summary>
        public string? DefaultPostExecutionScript { get; set; }
    }
}