using PKISharp.WACS.Configuration;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns;

/// <summary>
/// This class is responsible for creating an options object, 
/// either interactively or from the command line
/// </summary>
internal class WebnamesOptionsFactory(ArgumentsInputService arguments) : PluginOptionsFactory<WebnamesOptions>
{
    /// <summary>
    /// Getter for the APIUsername argument, which is required.
    /// </summary>
    private ArgumentResult<string?> APIUsername => arguments.
        GetString<WebnamesArguments>(a => a.APIUsername).
        Required();

    /// <summary>
    /// Getter for the APIKey argument, which is a protected
    /// string so the user can enter vault:// references and is also
    /// offered to store the value into the secret manager.
    /// </summary>
    private ArgumentResult<ProtectedString?> APIKey => arguments.
        GetProtectedString<WebnamesArguments>(a => a.APIKey).
        Required();

    /// <summary>
    /// Getter for the APIOverrideBaseURL argument, which is optional.
    /// </summary>
    private ArgumentResult<string?> APIOverrideBaseURL => arguments.
        GetString<WebnamesArguments>(a => a.APIOverrideBaseURL);

    /// <summary>
    /// Getter for the TTL argument, which is optional. When omitted,
    /// WebnamesClient.i_DEFAULT_TTL_SECONDS is used at validation time.
    /// </summary>
    private ArgumentResult<int?> TTL => arguments.
        GetInt<WebnamesArguments>(a => a.TTL).
        Validate(
            ttl => Task.FromResult(WebnamesClient.ai_SUPPORTED_TTL_SECONDS.Contains(ttl!.Value)),
            $"TTL must be one of the supported values ({string.Join(", ", WebnamesClient.ai_SUPPORTED_TTL_SECONDS)})");

    /// <summary>
    /// This is called when the user is setting up a new renewal interactively
    /// You have access to the input service, which allows you to ask questions
    /// and prompt feedback to the screen.
    /// </summary>
    public override async Task<WebnamesOptions?> Aquire(IInputService input, RunLevel runLevel)
    {
        return new WebnamesOptions()
        {
            APIUsername = await APIUsername.Interactive(input).GetValue(),
            APIKey = await APIKey.Interactive(input).GetValue(),
            APIOverrideBaseURL = await APIOverrideBaseURL.Interactive(input).GetValue(),
            TTL = await TTL.Interactive(input).GetValue()
        };
    }

    /// <summary>
    /// This is called when the user is setting up a new renewal from the
    /// command line. No user input is possible.
    /// </summary>
    public override async Task<WebnamesOptions?> Default()
    {
        return new WebnamesOptions()
        {
            APIUsername = await APIUsername.GetValue(),
            APIKey = await APIKey.GetValue(),
            APIOverrideBaseURL = await APIOverrideBaseURL.GetValue(),
            TTL = await TTL.GetValue()
        };
    }

    /// <summary>
    /// This method is used to describe the current configuration when 
    /// the user goes to Manage Renewals => Show Details.
    /// </summary>
    public override IEnumerable<(CommandLineAttribute, object?)> Describe(WebnamesOptions options)
    {
        yield return (APIUsername.Meta, options.APIUsername);
        yield return (APIKey.Meta, options.APIKey);
        yield return (APIOverrideBaseURL.Meta, options.APIOverrideBaseURL);
        yield return (TTL.Meta, options.TTL);
    }
}
