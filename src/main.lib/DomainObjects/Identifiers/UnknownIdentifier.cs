namespace PKISharp.WACS.DomainObjects.Identifiers
{
    public class UnknownIdentifier(string value) : Identifier(value, IdentifierType.Unknown)
    {
    }

}
