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
        /// 
        string? DefaultPostExecutionScript { get; }
        /// <summary>
        /// Default script to run after execution a renewal
        /// </summary>
        string? DefaultPreExecutionScript { get; }
    }

    internal class ExecutionSettings : IExecutionSettings
    {
        public string? DefaultPreExecutionScript { get; set; }

        public string? DefaultPostExecutionScript { get; set; }
    }
}