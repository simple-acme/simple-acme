using System;
using System.Linq;
using System.Net;

namespace PKISharp.WACS.DomainObjects.Identifiers
{
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

}
