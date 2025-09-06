using ACMESharp.Authorizations;
using PKISharp.WACS.Context;
using PKISharp.WACS.Plugins.ValidationPlugins.Http;
using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Any
{
    internal partial class ScriptHttp(
        Script parent,
        ValidationContext context,
        HttpValidationParameters pars,
        Http01ChallengeValidationDetails details) : HttpValidation<ManualOptions>(new ManualOptions(), pars)
    {
        internal const string DefaultPrepareArguments = "prepare {Identifier} {Path} {Token}";
        internal const string DefaultCleanupArguments = "cleanup {Identifier} {Path} {Token}";

        protected override Task DeleteFolder(string path) => Task.CompletedTask;
        protected override Task<bool> IsEmpty(string path) => Task.FromResult(true);

        protected override async Task WriteFile(string path, string content) =>
             await parent.Create(context.Identifier, path, details.HttpResourceValue);

        protected override async Task DeleteFile(string path) =>    
            await parent.Delete(context.Identifier, path, details.HttpResourceValue);

        internal Dictionary<string, string?> ReplaceTokens(string identifier, string path, bool censor, string token)
        {
            return new Dictionary<string, string?>
            {
                { "Identifier", identifier },
                { "Path", path },
                { "FileName", path.Split(PathSeparator)[^1] },
                { "Token", censor ? "***" : token }
            };
        }

        internal static void ExplainReplacements(IInputService input)
        {
            input.Show("{Identifier}", "Identifier that is being validated, e.g. sub.example.com");
            input.Show("{Path}", "Relative URI that will be requested");
            input.Show("{FileName}", "Name of the file that will be request");
            input.Show("{Token}", "Expected response content");
            input.Show("{vault://json/key}", "Secret from the vault");
        }
    }
}