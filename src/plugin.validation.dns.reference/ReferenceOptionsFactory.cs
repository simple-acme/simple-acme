using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    /// <summary>
    /// This class is responsible for creating an options object, 
    /// eiter interactively or from the command line
    /// </summary>
    internal class ReferenceOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<ReferenceOptions>
    {
        /// <summary>
        /// Getter for the ClientId argument, which is required.
        /// </summary>
        private ArgumentResult<string?> ClientId => arguments.
            GetString<ReferenceArguments>(a => a.ClientId).
            Required();

        /// <summary>
        /// Getter for the ClientSecret argument, which is a protected
        /// string so the user can enter vault:// references and is also
        /// offered to store the value into the secret manager.
        /// </summary>
        private ArgumentResult<ProtectedString?> ClientSecret => arguments.
            GetProtectedString<ReferenceArguments>(a => a.ClientSecret).
            Required();

        /// <summary>
        /// This is called when the user is setting up a new renewal interactively
        /// You have access to the input service, which allows you to ask questions
        /// and prompt feedback to the screen.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="runLevel"></param>
        /// <returns></returns>
        public override async Task<ReferenceOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            return new ReferenceOptions()
            {
                ClientId = await ClientId.Interactive(input).GetValue(),
                ClientSecret = await ClientSecret.Interactive(input).GetValue(),
            };
        }

        /// <summary>
        /// This is called when the user is setting up a new renewal from the
        /// command line. No user input is possible.
        /// </summary>
        /// <returns></returns>
        public override async Task<ReferenceOptions?> Default()
        {
            return new ReferenceOptions()
            {
                ClientId = await ClientId.GetValue(),
                ClientSecret = await ClientSecret.GetValue(),
            };
        }

        /// <summary>
        /// This method is used to describe the current configuration when 
        /// the user goes to Manage Renewals => Show Details.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public override IEnumerable<(CommandLineAttribute, object?)> Describe(ReferenceOptions options)
        {
            yield return (ClientId.Meta, options.ClientId);
            yield return (ClientSecret.Meta, options.ClientSecret);
        }
    }
}
