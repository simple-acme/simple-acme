using PKISharp.WACS.DomainObjects;
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Extensions
{
    public static class TargetExtensions
    {
        /// <summary>
        /// Parse unique identifiers that the certificate should be created for
        /// </summary>
        /// <returns></returns>
        public static List<Identifier> GetIdentifiers(this Target target) =>
            [.. target.Parts.SelectMany(GetIdentifiers).Distinct()];

        /// <summary>
        /// Parse unique identifiers that the certificate should be created for
        /// </summary>
        public static List<Identifier> GetIdentifiers(this TargetPart part) => 
            [.. part.Identifiers.Distinct()];

    }
}