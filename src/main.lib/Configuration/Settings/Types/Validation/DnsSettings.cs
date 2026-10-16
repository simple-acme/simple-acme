
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Configuration.Settings.Types.Validation
{
    public interface IDnsSettings
    {
        IEnumerable<string> Servers { get; }
        IPreValidationSettings PreValidation { get; }
        ISubstitutionSettings Substitution { get; }
    }

    internal class InheritDnsSettings(params IEnumerable<DnsSettings?> chain) : InheritSettings<DnsSettings>(chain), IDnsSettings
    {
        public IEnumerable<string> Servers => Get(x => x.Servers) ?? ["[System]"];
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
    }
}
