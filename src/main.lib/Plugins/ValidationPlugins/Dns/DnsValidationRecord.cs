using PKISharp.WACS.Clients.DNS;
using PKISharp.WACS.Context;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    /// <summary>
    /// Keep track of which records are created, so that they can be deleted later
    /// </summary>
    public class DnsValidationRecord
    {
        internal DnsValidationRecord(ValidationContext context, DnsLookupResult authority, string value)
        {
            Identifier = context.Identifier;
            Label = context.Label;
            Authority = authority;
            Value = value;
        }

        public string Label { get; }
        public string Identifier { get; }
        public DnsLookupResult Authority { get; }
        public string Value { get; }
    }
}
