using ACMESharp.Authorizations;
using PKISharp.WACS.Services;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    public abstract class HttpValidationBase(ILogService log, RunLevel runLevel, IInputService input): 
        Validation<Http01ChallengeValidationDetails>
    {
        protected readonly ILogService log = log;

        public async Task TestChallenge(Http01ChallengeValidationDetails challenge)
        {
            log.Information("Answer should now be browsable at {answerUri}", challenge.HttpResourceUrl);
            if (runLevel.HasFlag(RunLevel.Test))
            {
                if (await input.PromptYesNo("[--test] Try in default browser?", false))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = challenge.HttpResourceUrl,
                        UseShellExecute = true
                    });
                    await input.Wait();
                }
            }
        }
    }
}
