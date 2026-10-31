using MimeKit.Encodings;
using PKISharp.WACS.Extensions;
using System;
using System.Diagnostics;
using System.Globalization;

namespace PKISharp.WACS.DomainObjects.Identifiers
{

    /// <summary>
    /// Represent a potentially internatoinal hostname where
    /// 
    /// </summary>
    [DebuggerDisplay("{Unicode}")]
    public class HostName : IEquatable<HostName>, IComparable, IComparable<HostName>
    {

        private static readonly IdnMapping _idn = new() { UseStd3AsciiRules = false };
        public static readonly HostName Emtpy = new(string.Empty, string.Empty) { IsEmtpy = true };

        public static HostName Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Emtpy;
            }
            if (value == "*")
            {
                return Emtpy;
            }
            try
            {
                var punycode = _idn.GetAscii(value);
                var unicode = _idn.GetUnicode(punycode).ToLowerInvariant();
                return new HostName(unicode, punycode) { IsValid = true };
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
        public bool IsEmtpy { get; private set; }
        public bool IsValid { get; private set; }
        public bool IsCommon => IsValid && Unicode.Length <= Constants.MaxCommonName;
        public bool IsWildcard => IsValid && Unicode.StartsWith("*.");
        public string Unicode { get; private set; }
        public string Punycode { get; private set; }
        public override string ToString() => Punycode;
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
