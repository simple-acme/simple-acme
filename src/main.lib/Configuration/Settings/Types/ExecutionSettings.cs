﻿using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types
{
    /// <summary>
    /// Settings regarding the execution of the renewal
    /// </summary>
    public interface IExecutionSettings
    {
        /// <summary>
        /// Default script to run after execution a renewal
        /// </summary>
        string? DefaultPostExecutionScript { get; }

        /// <summary>
        /// Default script to run before executing a renewal
        /// </summary>
        string? DefaultPreExecutionScript { get; }
    }

    internal class InheritExecutionSettings(params IEnumerable<ExecutionSettings?> chain) : InheritSettings<ExecutionSettings>(chain), IExecutionSettings
    {
        public string? DefaultPostExecutionScript => Get(x => x.DefaultPostExecutionScript);
        public string? DefaultPreExecutionScript => Get(x => x.DefaultPreExecutionScript);
    }

    public class ExecutionSettings
    {
        [SettingsValue(
            SubType = "path",
            Description = "Path to a script that is executed before renewing a certificate.",
            Tip = "This may be useful to temporarely relax security measures, e.g. opening port 80 on the firewall. See <code>/Script/OpenIIS80FWrule.ps1</code> in the program directory for an example."
        )]
        public string? DefaultPreExecutionScript { get; set; }

        [SettingsValue(
            SubType = "path",
            Description = "Path to a script that is called after renewing a certificate, this may be useful to undo any actions taken by the script configured as the <code>DefaultPreExecutionScript</code>. Not to be confused with the <a href=\"/reference/plugins/installation/script\">script installation</a> plugin. The difference is that the installation plugin can be configured separately for each renewal and has access to a lot more context about the new and previous certificates. Also when the installation script fails, the renewal will be retried later. That is not the case for the pre/post execution scripts. Any errors there are logged but otherwise ignored."
        )]
        public string? DefaultPostExecutionScript { get; set; }
    }
}