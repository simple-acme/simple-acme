using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types.Validation
{
    public interface IPreValidationSettings
    {      
        /// <summary>
        /// If set to `true`, it will wait until it can verify that the 
        /// validation record has been created and is available before 
        /// beginning DNS validation.
        /// </summary>
        bool Enabled { get; }

        /// <summary>
        /// Add the local DNS server to the list of servers to query during prevalidation
        /// </summary>
        bool CheckLocal { get; }

        /// <summary>
        /// Add the authoritative DNS servers to the list of servers to query during prevalidation
        /// </summary>
        bool CheckAuthoritative { get; }

        /// <summary>
        /// Maximum numbers of times to retry DNS pre-validation, while
        /// waiting for the name servers to start providing the expected
        /// answer.
        /// </summary>
        int RetryCount { get; }

        /// <summary>
        /// Amount of time in seconds to wait between each retry.
        /// </summary>
        int RetryInterval { get; }
    }

    internal class InheritPreValidationSettings(params IEnumerable<PreValidationSettings?> chain) : InheritSettings<PreValidationSettings>(chain), IPreValidationSettings
    {
        public bool Enabled => Get(x => x.Enabled) ?? true;
        public bool CheckLocal => Get(x => x.CheckLocal) ?? false;
        public bool CheckAuthoritative => Get(x => x.CheckAuthoritative) ?? true;
        public int RetryCount => Get(x => x.RetryCount) ?? 5;
        public int RetryInterval => Get(x => x.RetryInterval) ?? 30;
    }

    public class PreValidationSettings
    {
        [SettingsValue(
            Default = "true",
            Description = "If set to <code>true</code>, the program will wait until it can verify that the proper record has been created before telling the server to start its independant validation.")]
        public bool? Enabled { get; set; }

        [SettingsValue(
            Default = "5",
            Description = "Maximum numbers of times to retry pre-validation, while waiting for the name servers to start providing the expected answer.")]
        public int? RetryCount { get; set; }

        [SettingsValue(
            Default = "30",
            Description = "Amount of time in seconds to wait between each retry.")]
        public int? RetryInterval { get; set; }

        [SettingsValue(
            Default = "false",
            Description = "Check the local DNS server(s).")]
        public bool? CheckLocal { get; set; }

        [SettingsValue(
            Default = "true",
            Description = "Check the authoritative DNS server(s).")]
        public bool? CheckAuthoritative { get; set; }
    }
}