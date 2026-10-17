
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Configuration.Settings.Types.Validation
{
    public interface IDnsSettings
    {
        IEnumerable<string> Servers { get; }
        int PropagationDelay { get; }
        IPreValidationSettings PreValidation { get; }
        ISubstitutionSettings Substitution { get; }
    }

    internal class InheritDnsSettings(params IEnumerable<DnsSettings?> chain) : InheritSettings<DnsSettings>(chain), IDnsSettings
    {
        public IEnumerable<string> Servers => Get(x => x.Servers) ?? ["[System]"];
        public int PropagationDelay => Get(x => x.PropagationDelay) ?? 0;
        public IPreValidationSettings PreValidation => new InheritPreValidationSettings(Chain.Select(c => c?.PreValidation));
        public ISubstitutionSettings Substitution => new InheritSubstitutionSettings(Chain.Select(c => c?.Substitution));
    }

    public class DnsSettings
    {
        [SettingsValue(
            SubType = "host",
            Default = "[ \"[System]\" ]",
            Description = "A list of servers that the program may query for DNS lookups. You can use the string <code>\"[System]\"</code> " +
            "to have the program use the defaults on your machine. Note that for some features like pre-validation " +
            "and substitution, the program might try to find and contact the authoritative name servers as well, which can lead to timeouts " +
            "if you block external requests. You can disable this behavior by setting <code>CheckAuthoritative</code> to <code>false</code> for " +
            "the respective features.")]
        public IEnumerable<string>? Servers { get; set; }
        public PreValidationSettings? PreValidation { get; set; }
        public SubstitutionSettings? Substitution { get; set; }
        [SettingsValue(
            Default = "0",
            Description = "Numbers of seconds to wait after creating new DNS records. This waiting time is introduced after the (optional) " +
            "pre-validation step, so it can be used as an alternative or in combination with it. Using both pre-validation and a PropagationDelay is " +
            "redundant in most cases, but in some cases we have found that providers need extra time to synchronise their global network of servers, " +
            "even after the geographically closest ones to the client have already started reflecting the required records.")]
        public int? PropagationDelay { get; set; }
    }
}
