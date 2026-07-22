using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns;

/// <summary>
/// This class is responsible for creating an options object,
/// either interactively or from the command line
/// </summary>
internal class IPProjectsOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<IPProjectsOptions>
{
    /// <summary>
    /// Getter for the ApiKey argument, which is a protected
    /// string so the user can enter vault:// references and is also
    /// offered to store the value into the secret manager.
    /// </summary>
    private ArgumentResult<ProtectedString?> ApiKey => arguments.
        GetProtectedString<IPProjectsArguments>(a => a.ApiKey).
        Required();

    /// <summary>
    /// This is called when the user is setting up a new renewal interactively
    /// </summary>
    /// <param name="input"></param>
    /// <param name="runLevel"></param>
    /// <returns></returns>
    public override async Task<IPProjectsOptions?> Aquire(IInputService input, RunLevel runLevel)
    {
        return new IPProjectsOptions()
        {
            ApiKey = await ApiKey.Interactive(input).GetValue(),
        };
    }

    /// <summary>
    /// This is called when the user is setting up a new renewal from the
    /// command line. No user input is possible.
    /// </summary>
    /// <returns></returns>
    public override async Task<IPProjectsOptions?> Default()
    {
        return new IPProjectsOptions()
        {
            ApiKey = await ApiKey.GetValue(),
        };
    }

    /// <summary>
    /// This method is used to describe the current configuration when
    /// the user goes to Manage Renewals => Show Details.
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    public override IEnumerable<(CommandLineAttribute, object?)> Describe(IPProjectsOptions options)
    {
        yield return (ApiKey.Meta, options.ApiKey);
    }
}
