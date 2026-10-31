using MimeKit;
using System;

namespace PKISharp.WACS.DomainObjects.Identifiers
{
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

}
