﻿using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types.Secrets
{
    /// <summary>
    /// Settings for json secret store
    /// </summary>
    public interface IJsonSettings
    {
        string? FilePath { get; }
    }

    internal class InheritJsonSettings(params IEnumerable<JsonSettings?> chain) : InheritSettings<JsonSettings>(chain), IJsonSettings
    {
        public string? FilePath => Get(x => x.FilePath);
    }

    public class JsonSettings 
    {
        [SettingsValue(
            SubType = "path",
            Description = "Location of the file store secrets.",
            NullBehaviour = "defaults to <code>{Client.ConfigurationPath}\\secrets.json</code>")]
        public string? FilePath { get; set; }
    }
}