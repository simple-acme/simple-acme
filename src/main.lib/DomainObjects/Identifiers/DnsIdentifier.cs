namespace PKISharp.WACS.DomainObjects.Identifiers
{
    public class DnsIdentifier : Identifier
    {
        public DnsIdentifier(string value) : base(value, IdentifierType.DnsName) => Host = HostName.Parse(value);
        public DnsIdentifier(HostName value) : base(value.Unicode, IdentifierType.DnsName) => Host = value;
        public HostName Host { get; }
        public override string Value => Host.Unicode;
    }

}
