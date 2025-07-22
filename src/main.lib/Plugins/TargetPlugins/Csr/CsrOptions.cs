﻿using PKISharp.WACS.Plugins.Base.Options;

namespace PKISharp.WACS.Plugins.TargetPlugins
{
    internal class CsrOptions : TargetPluginOptions
    {
        public const string Trigger = "CSR";
        public string? CsrFile { get; set; }
        public string? CsrScript { get; set; }
        public string? PkFile { get; set; }
    }
}
