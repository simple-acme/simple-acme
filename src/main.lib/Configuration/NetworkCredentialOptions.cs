using PKISharp.WACS.Extensions;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PKISharp.WACS.Configuration
{
    public interface INetworkCredentialArguments : IArguments
    {
        public string? UserName { get; }
        public string? Password { get; }
    }

    public class NetworkCredentialOptions
    {
        public string? UserName { get; set; }

        [JsonPropertyName("PasswordSafe")]
        public ProtectedString? Password { get; set; }

        public NetworkCredential GetCredential(
            SecretServiceManager secretService) =>
            new(UserName, secretService.EvaluateSecret(Password?.Value));

        public void Show(IInputService input)
        {
            input.Show("Username", UserName);
            input.Show("Password", Password?.DisplayValue);
        }

        public NetworkCredentialOptions() { }

        public NetworkCredentialOptions(string? userName, string? password) : this(userName, password.Protect()) { }
        public NetworkCredentialOptions(string? userName, ProtectedString? password)
        {
            UserName = userName;
            Password = password;
        }

        public static async Task<NetworkCredentialOptions> Create<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(ArgumentsInputService arguments) 
            where T : class, INetworkCredentialArguments, new()
        {
            return new NetworkCredentialOptions(
                await arguments.GetString<T>(x => x.UserName).GetValue(),
                await arguments.GetProtectedString<T>(x => x.Password).GetValue()
            );
        }

        public static async Task<NetworkCredentialOptions> Create<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(ArgumentsInputService arguments, IInputService input, string purpose)
            where T : class, INetworkCredentialArguments, new()
        {
            return new NetworkCredentialOptions(
                await arguments.GetString<T>(x => x.UserName).Interactive(input, purpose + " username").GetValue(),
                await arguments.GetProtectedString<T>(x => x.Password).Interactive(input, purpose + "password").GetValue()
            );
        }

        public IEnumerable<(CommandLineAttribute, object?)> Describe<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties)] T>(ArgumentsInputService arguments)
            where T : class, INetworkCredentialArguments, new()
        {
            yield return (arguments.GetString<T>(x => x.UserName).Meta, UserName);
            yield return (arguments.GetString<T>(x => x.Password).Meta, Password);
        }
    }
}
