using PKISharp.WACS.Configuration;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Factories;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TransIp.Library;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Dns
{
    internal sealed class TransIpOptionsFactory(
        ArgumentsInputService arguments,
        SecretServiceManager ssm,
        ILogService log,
        IProxyService proxy) : PluginOptionsFactory<TransIpOptions>
    {
        private ArgumentResult<string?> Login => arguments.
            GetString<TransIpArguments>(a => a.Login).
            Required();

        private ArgumentResult<ProtectedString?> PrivateKey => arguments.
            GetProtectedString<TransIpArguments>(a => a.PrivateKey).
            Validate(async x => await CheckKey(await ssm.EvaluateSecret(x?.Value)), "invalid private key").
            Required();

        public override async Task<TransIpOptions?> Aquire(IInputService input, RunLevel runLevel)
        {
            return new TransIpOptions()
            {
                Login = await Login.Interactive(input).WithLabel("Username").GetValue(),
                PrivateKey = await PrivateKey.Interactive(input, multiline: true).WithLabel("Private key").GetValue()
            };
        }

        public override async Task<TransIpOptions?> Default()
        {
            var login = await Login.GetValue();

            var keyFile = await arguments.
                GetString<TransIpArguments>(a => a.PrivateKeyFile).
                Validate(x => Task.FromResult(x.ValidFile(log)), "file doesn't exist").
                Validate(async x => await CheckKey(await File.ReadAllTextAsync(x!)), "invalid key").
                GetValue();

            var key = keyFile != null
                ? (await File.ReadAllTextAsync(keyFile)).Protect()
                : await PrivateKey.GetValue();

            return new TransIpOptions()
            {
                Login = login,
                PrivateKey = key
            };
        }

        private async Task<bool> CheckKey(string? privateKey)
        {
            if (privateKey == null)
            {
                return false;
            }
            try
            {
                var httpClient = await proxy.GetHttpClient();
                _ = new AuthenticationService("check", privateKey, httpClient);
                return true;
            }
            catch (Exception ex) 
            {
                log.Error(ex, "Invalid private key");
            }
            return false;
        }

        public override IEnumerable<(CommandLineAttribute, object?)> Describe(TransIpOptions options)
        {
            yield return (Login.Meta, options.Login);
            yield return (PrivateKey.Meta, options.PrivateKey);
        }
    }
}
