using System;
using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings
{
    public interface IScheduledTaskSettings
    {
        /// <summary>
        /// Configures time after which the scheduled 
        /// task will be terminated if it hangs for
        /// whatever reason.
        /// </summary>
        TimeSpan ExecutionTimeLimit { get; }

        /// <summary>
        /// Configures random time to wait for starting 
        /// the scheduled task.
        /// </summary>
        TimeSpan RandomDelay { get; }

        /// <summary>
        /// The number of days to renew a certificate 
        /// after. Let’s Encrypt certificates are 
        /// currently for a max of 90 days so it is 
        /// advised to not increase the days much.
        /// If you increase the days, please note
        /// that you will have less time to fix any
        /// issues if the certificate doesn’t renew 
        /// correctly.
        /// </summary>
        int RenewalDays { get; }

        /// <summary>
        /// To spread service load, program run time and/or to minimize 
        /// downtime, those managing a large amount of renewals may want
        /// to spread them out of the course of multiple days/weeks. 
        /// The number of days specified here will be substracted from
        /// RenewalDays to create a range in which the renewal will
        /// be processed. E.g. if RenewalDays is 66 and RenewalDaysRange
        /// is 10, the renewal will be processed between 45 and 55 days
        /// after issuing. 
        /// 
        /// If you use an order plugin to split your renewal into 
        /// multiple orders, orders may run on different days.
        /// </summary>
        int RenewalDaysRange { get; }

        /// <summary>
        /// By default we use ARI to manage renewal period (if available
        /// on the endpoint). This switch allows users to disable it.
        /// https://datatracker.ietf.org/doc/draft-ietf-acme-ari/
        /// </summary>
        bool RenewalDisableServerSchedule { get; }

        /// <summary>
        /// If a certificate is valid for less time than
        /// specified in RenewalDays it is at risk of expiring.
        /// E.g. a certificate valid for 30 days, would be invalid
        /// for 15 days already before it would be renewed at 
        /// 55 days. This is of course undesirable, so this setting
        /// defines the minimum number of valid days that the 
        /// certificate should have left. E.g. when the setting is 7,
        /// any certificate due to expire in less than 7 days will be
        /// renewed, regardless of when they were created.
        /// </summary>
        int RenewalMinimumValidDays { get; }

        /// <summary>
        /// Configures start time for the scheduled task.
        /// </summary>
        TimeSpan StartBoundary { get; }
    }

    internal class InheritScheduledTaskSettings(params IEnumerable<ScheduledTaskSettings?> chain) : InheritSettings<ScheduledTaskSettings>(chain), IScheduledTaskSettings
    {
        public TimeSpan ExecutionTimeLimit => Get(x => x.ExecutionTimeLimit) ?? TimeSpan.Zero;
        public TimeSpan RandomDelay => Get(x => x.RandomDelay) ?? TimeSpan.Zero;
        public int RenewalDays => Get(x => x.RenewalDays) ?? 55;
        public int RenewalDaysRange => Get(x => x.RenewalDaysRange) ?? 0;
        public bool RenewalDisableServerSchedule => Get(x => x.RenewalDisableServerSchedule) ?? false;
        public int RenewalMinimumValidDays => Get(x => x.RenewalMinimumValidDays) ?? 7;
        public TimeSpan StartBoundary => Get(x => x.StartBoundary) ?? TimeSpan.Zero;
    }

    internal class ScheduledTaskSettings
    {
        public int? RenewalDays { get; set; }
        public int? RenewalMinimumValidDays { get; set; }
        public int? RenewalDaysRange { get; set; }
        public bool? RenewalDisableServerSchedule { get; set; }
        public TimeSpan? RandomDelay { get; set; }
        public TimeSpan? StartBoundary { get; set; }
        public TimeSpan? ExecutionTimeLimit { get; set; }
    }
}