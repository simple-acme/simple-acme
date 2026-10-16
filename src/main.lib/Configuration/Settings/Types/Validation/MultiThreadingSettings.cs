using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types.Validation
{
    public interface IMultiThreadingSettings
    {
        /// <summary>
        /// Enable multithreading for validation
        /// </summary>
        bool Enabled { get; }
        /// <summary>
        /// Max number of validations to run in parallel
        /// </summary>
        int BatchSize { get; }
    }

    internal class InheritMultiThreadingSettings(params IEnumerable<MultiThreadingSettings?> chain) : InheritSettings<MultiThreadingSettings>(chain), IMultiThreadingSettings
    {
        public bool Enabled => Get(x => x.Enabled) ?? false;
        public int BatchSize => Get(x => x.BatchSize) ?? 100;
    }

    public class MultiThreadingSettings
    {
        [SettingsValue(
            Default = "true",
            Description = "Enable parallel validation, allowing the program to process multiple validations simultaneously instead of in sequential order. This can significantly speed up the process of requesting certificates with a large number of host names.",
            Warning = "Not all validation methods and plugins support parallelism, so actual results may vary.")]
        public bool? Enabled { get; set; }

        [SettingsValue(
            Default = "20",
            Description = "Maximum number of validations to run in parallel. Reasons to limit the batch size can be to avoid hitting certain limits (e.g. maximum number of TXT records under one domain).")]
        public int? BatchSize { get; set; }
    }
}