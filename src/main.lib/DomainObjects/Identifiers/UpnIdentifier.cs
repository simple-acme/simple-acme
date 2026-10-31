namespace PKISharp.WACS.DomainObjects.Identifiers
{
    public class UpnIdentifier(string value) : Identifier(value, IdentifierType.UpnName)
    {
    }

}
