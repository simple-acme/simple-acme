using System;
using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types
{
    public interface IScheduledTaskSettings
    {
        TimeSpan ExecutionTimeLimit { get; }
        TimeSpan RandomDelay { get; }
        int? RenewalPercentage { get; }
        int? RenewalDays { get; }
        int? RenewalDaysRange { get; }
        bool RenewalDisableServerSchedule { get; }
        int? RenewalMinimumValidDays { get; }
        TimeSpan StartBoundary { get; }
    }

    internal class InheritScheduledTaskSettings(params IEnumerable<ScheduledTaskSettings?> chain) : InheritSettings<ScheduledTaskSettings>(chain), IScheduledTaskSettings
    {
        public TimeSpan ExecutionTimeLimit => Get(x => x.ExecutionTimeLimit) ?? TimeSpan.Zero;
        public TimeSpan RandomDelay => Get(x => x.RandomDelay) ?? TimeSpan.Zero;
        public int? RenewalPercentage => Get(x => x.RenewalPercentage);
        public int? RenewalDays => Get(x => x.RenewalDays);
        public int? RenewalDaysRange => Get(x => x.RenewalDaysRange);
        public bool RenewalDisableServerSchedule => Get(x => x.RenewalDisableServerSchedule) ?? false;
        public int? RenewalMinimumValidDays => Get(x => x.RenewalMinimumValidDays);
        public TimeSpan StartBoundary => Get(x => x.StartBoundary) ?? TimeSpan.Zero;
    }

    public class ScheduledTaskSettings
    {
        [SettingsValue(
            Default = "70",
            NullBehaviour = "fallback to <code>RenewalDays</code> guarded by <code>RenewalMinimumValidDays</code>.", 
            Description = "Trigger the renewal of any certificate that has passed the specified percentage of " +
            "its lifetime. For example for a certificate that is valid for 100 days, setting this to 75 will " +
            "trigger the renewal after 75 days.",
            Warning = "If you increase the percentage, please note that you will have less time to fix any issues " +
            "if the certificate doesn't renew correctly.")]
        public int? RenewalPercentage { get; set; }

        [SettingsValue(
            Default = null,
            NullBehaviour = "equivalent to 160 days",   
            Description = "Trigger the renewal of any certificate that has passed the specified number of days of its lifetime. " +
            "Currently publically trusted certificates have a maximum lifetime of 200 days, so we use 160 days as a default to " +
            "have a grace period of a little more than a month.",
            Warning = "If you increase the days, please note that you will have less time to fix any issues if the certificate doesn't renew correctly.")]
        public int? RenewalDays { get; set; }

        [SettingsValue(
            Default = "0",
            Description = "To spread service load, program run time and/or to minimize downtime, those managing a " +
            "large amount of renewals may want to spread them out of the course of multiple days/weeks. The " +
            "number of days specified here will be substracted from the date determined by <code>RenewalPercentage</code> " +
            "or the legaccy <code>RenewalDays</code> to create a range within which the renewal will be processed. " +
            "E.g. if the computed reneal should happen after 40 days and <code>RenewalDaysRange</code> is <code>10</code>, " +
            "the renewal will be processed between <code>30</code> and <code>40</code> days after issuing. If you use an order " +
            "plugin to split your renewal into multiple orders, different orders may run on different days.")]
        public int? RenewalDaysRange { get; set; }

        [SettingsValue(
            Default = "false",
            Description = "By default, servers implementing ARI may suggest that renewals should happen earlier than the " +
            "regularly scheduled moment. When set to <code>true</code>, ARI suggestions will be ignored (not recommended).")]
        public bool? RenewalDisableServerSchedule { get; set; }

        [SettingsValue(
            Default = null,
            NullBehaviour = "equivalent to 7 days",
            Description = "The minimum number of days a certificate should still be valid. If the certificate is valid " +
            "for a smaller number of days, it will be renewed regardless of the <code>RenewalDays</code> setting.")]
        public int? RenewalMinimumValidDays { get; set; }

        [SettingsValue(
            Default = "'09:00:00'",
            DefaultExtra = "9:00 am",
            Description = "Configures start time for the scheduled task.")]
        public TimeSpan? StartBoundary { get; set; }

        [SettingsValue(
            Default = "'02:00:00'",
            DefaultExtra = "2 hours",
            Description = "Configures time after which the scheduled task will be terminated if it hangs for whatever reason.")]
        public TimeSpan? ExecutionTimeLimit { get; set; }

        [SettingsValue(
            Default = "'04:00:00'",
            DefaultExtra = "4 hours",
            Description = "Configures random time to wait for starting the scheduled task. This spreads the load on the servers " +
            "and thus prevents users from getting <code>TooManyRequests</code> errors.")]
        public TimeSpan? RandomDelay { get; set; }
    }
}