using ACMESharp.Protocol.Resources;
using System;
using System.Diagnostics;
using System.Net;

namespace PKISharp.WACS.DomainObjects.Identifiers
{
    public enum IdentifierType
    {
        Unknown,
        IpAddress,
        DnsName,
        UpnName,
        Email
    }

    [DebuggerDisplay("{Type}: {Value}")]
    public abstract class Identifier(string value, IdentifierType identifierType = IdentifierType.Unknown) : IEquatable<Identifier>, IComparable, IComparable<Identifier>
    {

        public static Identifier Parse(AcmeIdentifier identifier, bool? wildcard = null)
        {
            return identifier.Type switch
            {
                "ip" => new IpIdentifier(identifier.Value),
                "dns" => new DnsIdentifier(wildcard == true ? $"*.{identifier.Value}" : identifier.Value),
                _ => new UnknownIdentifier(identifier.Value)
            };
        }

        public static Identifier Parse(AcmeAuthorization authorization)
        {
            if (authorization.Identifier == null)
            {
                throw new NotSupportedException("Missing identifier");
            }
            return Parse(authorization.Identifier, authorization.Wildcard);
        }

        public static Identifier Parse(string identifier)
        {
            if (IPAddress.TryParse(identifier, out var address))
            {
                return new IpIdentifier(address);
            }
            return new DnsIdentifier(identifier);
        }

        public IdentifierType Type { get; } = identifierType;
        public virtual string Value { get; internal set; } = value;
        public override string ToString() => $"{Type}: {Value}";
        public override bool Equals(object? obj) => (obj as Identifier) == this;
        public override int GetHashCode() => ToString().GetHashCode();
        public bool Equals(Identifier? other) => other == this;
        public int CompareTo(object? obj) => ToString().CompareTo((obj as Identifier)?.ToString());
        public int CompareTo(Identifier? other) => ToString().CompareTo(other?.ToString());
        public static bool operator ==(Identifier? a, Identifier? b) => string.Equals(a?.ToString(), b?.ToString(), StringComparison.OrdinalIgnoreCase);
        public static bool operator !=(Identifier? a, Identifier? b) => !(a == b);
    }

}
