﻿using PKISharp.WACS.Configuration;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.Azure.Common
{
    /// <summary>
    /// Azure common options
    /// </summary>
    public class AzureOptionsFactoryCommon<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(ArgumentsInputService arguments) where T: AzureArgumentsCommon, new()
    {
        private ArgumentResult<string?> Environment => arguments.
            GetString<T>(a => a.AzureEnvironment);

        private ArgumentResult<bool?> UseMsi => arguments.
            GetBool<T>(a => a.AzureUseMsi);

        private ArgumentResult<string?> TenantId => arguments.
            GetString<T>(a => a.AzureTenantId).
            Required();

        private ArgumentResult<string?> ClientId => arguments.
            GetString<T>(a => a.AzureClientId).
            Required();

        private ArgumentResult<ProtectedString?> ClientSecret => arguments.
            GetProtectedString<T>(a => a.AzureSecret).
            Required();

        public async Task Aquire(IAzureOptionsCommon options, IInputService _input)
        {
            var defaultEnvironment = (await Environment.GetValue()) ?? AzureEnvironments.AzureCloud;
            var environments = new List<Choice<Func<Task>>>(
                AzureEnvironments.ResourceManagerUrls()
                    .OrderBy(kvp => kvp.Key)
                    .Select(kvp =>
                        Choice.Create<Func<Task>>(() =>
                        {
                            options.AzureEnvironment = kvp.Key;
                            return Task.CompletedTask;
                        },
                    description: kvp.Key,
                    @default: kvp.Key == defaultEnvironment)))
            {
                Choice.Create<Func<Task>>(async () => await AzureOptionsFactoryCommon<T>.InputUrl(_input, options), "Use a custom resource manager url")
            };
            var chosen = await _input.ChooseFromMenu("Which Azure environment are you using?", environments);
            await chosen.Invoke();

            options.UseMsi = 
                await UseMsi.GetValue() == true || 
                await _input.PromptYesNo("Do you want to use a managed service identity?", false);

            if (!options.UseMsi)
            {
                // These options are only necessary for client id/secret authentication.
                options.TenantId = await TenantId.Interactive(_input).WithLabel("Directory/tenant id").GetValue();
                options.ClientId = await ClientId.Interactive(_input).WithLabel("Application client id").GetValue();
                options.Secret = await ClientSecret.Interactive(_input).WithLabel("Application client secret").GetValue();
            }
        }

        public async Task Default(IAzureOptionsCommon options)
        {
            options.UseMsi = await UseMsi.GetValue() == true;
            options.AzureEnvironment = await Environment.GetValue();
            if (!options.UseMsi)
            {
                // These options are only necessary for client id/secret authentication.
                options.TenantId = await TenantId.GetValue();
                options.ClientId = await ClientId.GetValue();
                options.Secret = await ClientSecret.GetValue();
            }
        }

        private static async Task InputUrl(IInputService input, IAzureOptionsCommon options)
        {
            string raw;
            do
            {
                raw = await input.RequestString("Url");
            }
            while (!ParseUrl(raw, options));
        }

        private static bool ParseUrl(string url, IAzureOptionsCommon options)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }
            try
            {
                var uri = new Uri(url);
                options.AzureEnvironment = uri.ToString();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        public IEnumerable<(CommandLineAttribute, object?)> Describe(IAzureOptionsCommon options)
        {
            yield return (Environment.Meta, options.AzureEnvironment);
            yield return (UseMsi.Meta, options.UseMsi);
            yield return (TenantId.Meta, options.TenantId);
            yield return (ClientId.Meta, options.ClientId);
            yield return (ClientSecret.Meta, options.Secret);
        }
    }
}
