using ACMESharp.Authorizations;
using PKISharp.WACS.Context;
using PKISharp.WACS.Plugins.ValidationPlugins.Http;
using PKISharp.WACS.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Any
{
    internal class ManualHttp(
        HttpValidationParameters pars,
        RunLevel runLevel,
        IInputService input) : HttpValidation<ManualOptions>(new ManualOptions(), runLevel, pars)
    {
        private ValidationContext? context = null;
        protected override Task DeleteFolder(string path) => Task.CompletedTask;
        protected override Task<bool> IsEmpty(string path) => Task.FromResult(true);

        public override async Task<bool> PrepareChallenge(ValidationContext context, Http01ChallengeValidationDetails challenge)
        {
            this.context = context;
            // Pre-pre-validate, allowing the manual user to correct mistakes
            while (true)
            {
                if (await base.PrepareChallenge(context, challenge))
                {
                    return true;
                }
                else
                {
                    input.CreateSpace();
                    input.Show(null, value: "The correct file has not yet been found by the local HTTP client. That means it's likely the validation attempt will fail, or your local network doesn't allow enable access to the site via its public IP.");
                    var options = new List<Choice<bool?>>
                    {
                        Choice.Create<bool?>(null, "Retry check"),
                        Choice.Create<bool?>(true, "Ignore and continue"),
                        Choice.Create<bool?>(false, "Abort")
                    };
                    var chosen = await input.ChooseFromMenu("How would you like to proceed?", options);
                    if (chosen != null)
                    {
                        return chosen.Value;
                    }
                }
            }
        }

        protected override async Task WriteFile(string path, string content)
        {
            input.CreateSpace();
            input.Show("Domain", context?.Label);
            input.Show("Path", path);
            input.Show("Content", content);
            input.CreateSpace();
            if (!await input.Wait("Please press <Enter> after you've created and verified the file"))
            {
                _log.Warning("User aborted");
                return;
            }
        }

        protected override async Task DeleteFile(string path)
        {
            input.CreateSpace();
            input.Show("Path", path);
            input.CreateSpace();
            await input.Wait("Please press <Enter> after you've deleted the file");
        }
    }
}