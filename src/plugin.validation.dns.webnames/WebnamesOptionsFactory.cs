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
internal class WebnamesOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<WebnamesOptions>
{
    /// <summary>
    /// Getter for the ClientId argument, which is required.
    /// </summary>
    private ArgumentResult<string?> APIUsername => arguments.
        GetString<WebnamesArguments>(a => a.APIUsername).
        Required();

    /// <summary>
    /// Getter for the ClientSecret argument, which is a protected
    /// string so the user can enter vault:// references and is also
    /// offered to store the value into the secret manager.
    /// </summary>
    private ArgumentResult<ProtectedString?> APIKey => arguments.
        GetProtectedString<WebnamesArguments>(a => a.APIKey).
        Required();

    /// <summary>
    /// Getter for the ClientSecret argument, which is a protected
    /// string so the user can enter vault:// references and is also
    /// offered to store the value into the secret manager.
    /// </summary>
    private ArgumentResult<string?> APIOverrideBaseURL => arguments.
        GetString<WebnamesArguments>(a => a.APIOverrideBaseURL);

    /// <summary>
    /// This is called when the user is setting up a new renewal interactively
    /// You have access to the input service, which allows you to ask questions
    /// and prompt feedback to the screen.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="runLevel"></param>
    /// <returns></returns>
    public override async Task<WebnamesOptions?> Aquire(IInputService input, RunLevel runLevel)
    {
        return new WebnamesOptions()
        {
            APIUsername = await APIUsername.Interactive(input).GetValue(),
            APIKey = await APIKey.Interactive(input).GetValue(),
            APIOverrideBaseURL = await APIOverrideBaseURL.Interactive(input).GetValue()
        };
    }

    /// <summary>
    /// This is called when the user is setting up a new renewal from the
    /// command line. No user input is possible.
    /// </summary>
    /// <returns></returns>
    public override async Task<WebnamesOptions?> Default()
    {
        return new WebnamesOptions()
        {
            APIUsername = await APIUsername.GetValue(),
            APIKey = await APIKey.GetValue(),
            APIOverrideBaseURL = await APIOverrideBaseURL.GetValue(),
        };
    }

    /// <summary>
    /// This method is used to describe the current configuration when 
    /// the user goes to Manage Renewals => Show Details.
    /// </summary>
    /// <param name="options"></param>
    /// <returns></returns>
    public override IEnumerable<(CommandLineAttribute, object?)> Describe(WebnamesOptions options)
    {
        yield return (APIUsername.Meta, options.APIUsername);
        yield return (APIKey.Meta, options.APIKey);
        yield return (APIOverrideBaseURL.Meta, options.APIOverrideBaseURL);
    }
}
