using System.Collections.Generic;

namespace PKISharp.WACS.Configuration.Settings.Types.Validation
{
    public interface ISubstitutionSettings
    {
        /// <summary>
        /// If set to `true`, the program will attempt to recurively 
        /// follow CNAME records present on _acme-challenge subdomains to 
        /// find the final domain the DNS-01 challenge should be handled by.
        /// This allows you to delegate validation of your certificates
        /// to another domain or provider, which can have benefits for 
        /// security or save you the effort of having to move everything 
        /// to a party that supports automation.
        /// </summary>
        bool Enabled { get; }

        /// <summary>
        /// Add the authoritative DNS servers to the list of servers to query during substitution 
        /// checks.
        /// </summary>
        bool CheckAuthoritative { get; }
    }

    internal class InheritSubstitutionSettings(params IEnumerable<SubstitutionSettings?> chain) : InheritSettings<SubstitutionSettings>(chain), ISubstitutionSettings
    {
        public bool Enabled => Get(x => x.Enabled) ?? true;
        public bool CheckAuthoritative => Get(x => x.CheckAuthoritative) ?? false;
    }

    public class SubstitutionSettings
    {
        [SettingsValue(
            Default = "true",
            Description = "If your goal is to get a certificate for <code>example.com</code> using DNS validation, " +
            "but the DNS service for that domain does not support automation, there is no plugin available for it " +
            "and/or your security policy doesn't allow third party tools like simple-acme to access the DNS " +
            "configuration, then you can set up a <code>CNAME</code> from <code>_acme-challenge.example.com</code> " +
            "to another (sub)domain under your control that doesn't have these limitations. " +
            "\n" +
            "<a href=\"/reference/plugins/validation/dns/acme-dns\">acme-dns</a> is based on this principle, " +
            "but the same trick can be applied to any of the <a href=\"/reference/plugins/validation/dns/\">DNS plugins</a>. " +
            "Set this value to <code>false</code> to disable the feature.",
            Warning = "Note that for the program to understand your DNS setup, the <code>CNAME</code> record will " +
            "have to visible to it. If you have a complicated DNS setup with an internal-facing \"split brain\" that " +
            "is lacking the relevant records, you can let simple-acme use a public DNS server like <code>1.1.1.1</code> " +
            "instead of your system server using the <code>DnsServers</code> setting.")]
        public bool? Enabled { get; set; }

        [SettingsValue(
            Default = "true",
            Description = "Try to contact authoritative DNS server(s) to find CNAME records, bypassing any local cache.")]
        public bool? CheckAuthoritative { get; set; }
    }
}