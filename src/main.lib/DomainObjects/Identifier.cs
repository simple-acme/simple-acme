using ACMESharp.Protocol.Resources;
using MimeKit;
using PKISharp.WACS.Extensions;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;

namespace PKISharp.WACS.DomainObjects
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

        public virtual Identifier Unicode(bool unicode) => this;

        public IdentifierType Type { get; set; } = identifierType;
        public string Value { get; set; } = value;
        public override string ToString() => $"{Type}: {Value}";
        public override bool Equals(object? obj) => (obj as Identifier) == this;
        public override int GetHashCode() => ToString().GetHashCode();
        public bool Equals(Identifier? other) => other == this;
        public int CompareTo(object? obj) => ToString().CompareTo((obj as Identifier)?.ToString());
        public int CompareTo(Identifier? other) => ToString().CompareTo(other?.ToString());
        public static bool operator ==(Identifier? a, Identifier? b) => string.Equals(a?.ToString(), b?.ToString(), StringComparison.OrdinalIgnoreCase);
        public static bool operator !=(Identifier? a, Identifier? b) => !(a == b);
    }

    public class DnsIdentifier(string value) : Identifier(value, IdentifierType.DnsName)
    {
        public override Identifier Unicode(bool unicode)
        {
            if (unicode)
            {
                return new DnsIdentifier(Value.ConvertPunycode());
            }
            else
            {
                var idn = new IdnMapping();
                return new DnsIdentifier(idn.GetAscii(Value));
            }
        }
    }

    public class IpIdentifier : Identifier
    {
        public IpIdentifier(IPAddress value) : base(value.ToString(), IdentifierType.IpAddress) {}

        public IpIdentifier(string value) : base(value, IdentifierType.IpAddress)
        {
            if (IPAddress.TryParse(value, out var parsed))
            {
                Value = parsed.ToString();
                return;
            }
            if (value.StartsWith('#'))
            {
                var hex = value.TrimStart('#');
                try
                {
                    var bytes = Enumerable.Range(0, hex.Length / 2).Select(x => Convert.ToByte(hex.Substring(x * 2, 2), 16)).ToArray();
                    var ip = new IPAddress(bytes);
                    Value = ip.ToString();
                    return;
                }
                catch {}
            }
            throw new ArgumentException("Value is not recognized as a valid IP address");
        }
    }
    public class EmailIdentifier : Identifier
    {
        public EmailIdentifier(string value) : base(value, IdentifierType.Email)
        {
            try
            {
                var sender = new MailboxAddress("Test", value);
            } 
            catch
            {
                throw new ArgumentException("Value is not recognized as a valid email address");
            }
        }
    }

    public class UpnIdentifier(string value) : Identifier(value, IdentifierType.UpnName)
    {
    }

    public class UnknownIdentifier(string value) : Identifier(value, IdentifierType.Unknown)
    {
    }

}
