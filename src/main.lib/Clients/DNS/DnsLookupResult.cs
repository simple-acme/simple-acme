using System.Collections.Generic;

namespace PKISharp.WACS.Clients.DNS
{
    public class DnsLookupResult(string domain, IEnumerable<LookupClientWrapper> nameServers, DnsLookupResult? cnameFrom = null)
    {
        public IEnumerable<LookupClientWrapper> Nameservers { get; set; } = nameServers;
        public string Domain { get; set; } = domain;
        public DnsLookupResult? From { get; set; } = cnameFrom;
    }
}
