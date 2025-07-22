using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Options;
using System.Text.RegularExpressions;

namespace PKISharp.WACS.Services
{

    /// <summary>
    /// Serialized data
    /// </summary>
    internal class GlobalValidationPluginOptions
    {
        /// <summary>
        /// Priority of this rule (lower number = higher priority)
        /// </summary>
        public int? Priority { get; set; }

        /// <summary>
        /// Direct input of a regular expression
        /// </summary>
        public string? Regex { get; set; }

        /// <summary>
        /// Input of a pattern like used in other
        /// parts of the software as well, e.g.
        /// </summary>
        public string? Pattern { get; set; }

        /// <summary>
        /// The actual validation options that 
        /// are stored for re-use
        /// </summary>
        public ValidationPluginOptions? ValidationPluginOptions { get; set; }

        /// <summary>
        /// Convert the user settings into a Regex that will be 
        /// matched with the identifier.
        /// </summary>
        private Regex? ParsedRegex()
        {
            if (Pattern != null)
            {
                return new Regex(Pattern.PatternToRegex());
            }
            if (Regex != null)
            {
                return new Regex(Regex);
            }
            return null;
        }

        /// <summary>
        /// Test if this specific identifier is a match
        /// for these validation options
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public bool Match(Identifier identifier)
        {
            var regex = ParsedRegex();
            if (regex == null)
            {
                return false;
            }
            return regex.IsMatch(identifier.Value);
        }
    }
}