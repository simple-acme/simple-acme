﻿using PKISharp.WACS.Configuration.Settings.Types.UI;
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Configuration.Settings.Types
{
    public interface IUiSettings
    {
        /// <summary>
        /// Which colors should be applied
        /// </summary>
        IColorSettings Color { get; }

        /// <summary>
        /// A string that is used to format the date of the 
        /// pfx file friendly name. Documentation for 
        /// possibilities is available from Microsoft.
        /// </summary>
        string? DateFormat { get; }

        /// <summary>
        /// The number of hosts to display per page.
        /// </summary>
        int PageSize { get; }

        /// <summary>
        /// How console tekst should be encoded
        /// </summary>
        string? TextEncoding { get; }
    }

    internal class InheritUiSettings(params IEnumerable<UiSettings?> chain) : InheritSettings<UiSettings>(chain), IUiSettings
    {
        public IColorSettings Color => new InheritColorSettings(Chain.Select(c => c?.Color));
        public string? DateFormat => Get(x => x.DateFormat);
        public int PageSize => Get(x => x.PageSize) ?? 50;
        public string? TextEncoding => Get(x => x.TextEncoding);
    }

    public class UiSettings
    {
        [SettingsValue(
            Default = "yyyy/M/d H:mm:ss",
            Description = "A string that is used to format dates in the user interface. " +
            "<a href=\"https://learn.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings\">Documentation</a> " +
            "for possibilities is available from Microsoft.",
            Tip = "A date in this format is also appended to the friendly name of generated certificates. " +
            "You can disable that using <code>{Security.FriendlyNameDateTimeStamp}</code>.")]
        public string? DateFormat { get; set; }

        [SettingsValue(
            Default = "50",
            Description = "The number of items to display per page in list views.")]
        public int? PageSize { get; set; }

        [SettingsValue(
            Default = "utf-8",
            Description = "Encoding to use for the console output. A list of possible values can be found " +
            "<a href=\"https://learn.microsoft.com/en-us/dotnet/api/system.text.encoding\">here</a>. For certain " +
            "languages <code>\"unicode\"</code> might give better results displaying the characters.",
            Warning = "Note that changing this setting reduces compatibility with other programs that may be " +
            "processing output generated by simple-acme.")]
        public string? TextEncoding { get; set; }

        public ColorSettings? Color { get; set; }
    }
}