using System;
using System.Diagnostics;
using System.Globalization;

namespace PKISharp.WACS.DomainObjects.Identifiers
{
    /// <summary>
    /// Represent a potentially international hostname, which can be represented
    /// in Unicode or Punycode. The Unicode representation is used for display purposes
    /// while the Punycode representation is used for certificate requests.
    /// </summary>
    [DebuggerDisplay("{Unicode}")]
    public class HostName : IEquatable<HostName>, IComparable, IComparable<HostName>
    {
        /// <summary>
        /// We parse wildcards, which do not meet UseStd3AsciiRules requirement
        /// </summary>
        private static readonly IdnMapping _idn = new() { UseStd3AsciiRules = false };
        public static readonly HostName Empty = new(string.Empty, string.Empty) { IsEmpty = true };

        public static HostName Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Empty;
            }
            if (value == "*")
            {
                return Empty;
            }
            try
            {
                var punycode = _idn.GetAscii(value.ToLowerInvariant());
                var unicode = _idn.GetUnicode(punycode).ToLowerInvariant();
                return new HostName(unicode, punycode);
            }
            catch (ArgumentException ex)
            {
                 throw new Exception($"Invalid value: {value}", ex);

            }
        }

        private HostName(string unicode, string punycode)
        {
            Unicode = unicode;
            Punycode = punycode;
        }
        public bool IsEmpty { get; private set; }
        public bool IsCommon => Punycode.Length <= Constants.MaxCommonName;
        public bool IsWildcard => Unicode.StartsWith("*.");
        public string Unicode { get; private set; }
        public string Punycode { get; private set; }
        public override string ToString() => Unicode;
        public override bool Equals(object? obj) => (obj as HostName) == this;
        public override int GetHashCode() => ToString().GetHashCode();
        public bool Equals(HostName? other) => other == this;
        public int CompareTo(object? obj) => ToString().CompareTo((obj as HostName)?.ToString());
        public int CompareTo(HostName? other) => ToString().CompareTo(other?.ToString());
        public static bool operator ==(HostName? a, HostName? b) => string.Equals(a?.ToString(), b?.ToString());
        public static bool operator !=(HostName? a, HostName? b) => !(a == b);
        public static implicit operator HostName(string v) => Parse(v);
    }
 }
