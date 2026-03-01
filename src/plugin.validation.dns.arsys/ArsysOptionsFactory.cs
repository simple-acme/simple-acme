using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    /// <summary>
    /// This class is responsible for creating an options object, 
    /// either interactively or from the command line
    /// </summary>
    internal class ArsysOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<ArsysOptions>
    {
        /// <summary>
        /// Getter for the DNSApiKey argument, which is a protected
        /// string so the user can enter vault:// references and is also
        /// offered to store the value into the secret manager.
        /// </summary>
        private ArgumentResult<ProtectedString?> DNSApiKey => arguments.
            GetProtectedString<ArsysArguments>(a => a.DNSApiKey).
            Required();

        /// <summary>
        /// This is called when the user is setting up a new renewal interactively
        /// You have access to the input service, which allows you to ask questions
        /// and prompt feedback to the screen.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        public override async Task<ArsysOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            return new ArsysOptions()
            {
                DNSApiKey = await DNSApiKey.Interactive(input).GetValue(),
            };
        }

        /// <summary>
        /// This is called when the user is setting up a new renewal from the
        /// command line. No user input is possible.
        /// </summary>
        /// <returns></returns>
        public override async Task<ArsysOptions?> Default()
        {
            return new ArsysOptions()
            {
                DNSApiKey = await DNSApiKey.GetValue(),
            };
        }

        /// <summary>
        /// This method is used to describe the current configuration when 
        /// the user goes to Manage Renewals => Show Details.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public override IEnumerable<(CommandLineAttribute, object?)> Describe(ArsysOptions options)
        {
            yield return (DNSApiKey.Meta, options.DNSApiKey);
        }
    }
}
