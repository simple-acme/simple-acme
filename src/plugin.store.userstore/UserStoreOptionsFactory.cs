using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.StorePlugins
{
    internal class UserStoreOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<UserStoreOptions>
    {
        private ArgumentResult<bool?> KeepExisting => arguments.
            GetBool<UserArguments>(x => x.KeepExisting).
            WithDefault(false).
            DefaultAsNull();

        public override async Task<UserStoreOptions?> Aquire(IInputService inputService, RunLevel runLevel) => 
            await Default();

        public override async Task<UserStoreOptions?> Default()
        {
            return new UserStoreOptions
            {
                KeepExisting = await KeepExisting.GetValue()
            };
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(UserStoreOptions options)
        {
            yield return (KeepExisting.Meta, options.KeepExisting);
        }
    }
}
