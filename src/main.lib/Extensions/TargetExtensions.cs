using PKISharp.WACS.DomainObjects;
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Extensions
{
    public static class TargetExtensions
    {
        /// <summary>
        /// Parse unique DNS identifiers that the certificate should be created for
        /// </summary>
        /// <param name="unicode"></param>
        /// <returns></returns>
        public static List<Identifier> GetIdentifiers(this Target target, bool unicode) => 
            target.Parts.SelectMany(x => x.GetIdentifiers(unicode)).Distinct().ToList();

        /// <summary>
        /// Parse unique DNS identifiers that the certificate should be created for
        /// </summary>
        /// <param name="unicode"></param>
        /// <returns></returns>
        public static List<Identifier> GetIdentifiers(this TargetPart part, bool unicode) => 
            part.Identifiers.Distinct().Select(x => x.Unicode(unicode)).ToList();

    }
}