using System;

namespace PKISharp.WACS
{
    /// <summary>
    /// Execution flags to enable/disable certain functions
    /// for different types of runs
    /// </summary>
    [Flags]
    public enum RunLevel
    {
        None = 0,
        Unattended = 1,
        Interactive = 2,
        Simple = 4,
        Advanced = 8,
        Test = 16,
        ForceValidation = 32,
        Force = 64,
        NoCache = 128,
        NoTaskScheduler = 256,
        ForceTaskScheduler = 512
    }

    [Flags]
    public enum Steps
    {
        None = 0,
        Source = 1,
        Order = 2,
        Csr = 4,
        Validation = 8,
        Store = 16,
        Installation = 32,
        Account = 64,
        Profile = 128,
        All = int.MaxValue
    }
}