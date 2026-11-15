using PKISharp.WACS.Clients.Acme;
using PKISharp.WACS.Configuration.Arguments;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services.Legacy
{
    internal class Importer(
        ILogService log, 
        IInputService input, 
        MainArguments arguments,
        IAutoRenewService currentTaskScheduler,
        AcmeClientManager clientManager)
    {
        public async Task Import(RunLevel runLevel)
        {
            if (!arguments.NoTaskScheduler)
            {
                log.Information("Step {x}/2: create new scheduled task", 1);
                await currentTaskScheduler.EnsureAutoRenew(runLevel | RunLevel.ForceTaskScheduler);
            }
            log.Information("Step {x}/2: ensure ACMEv2 account", 2);
            await clientManager.GetClient(runLevel);
            var listCommand = "--list";
            var renewCommand = "--renew";
            if (runLevel.HasFlag(RunLevel.Interactive))
            {
                listCommand = "Manage renewals";
                renewCommand = "Run";
            }
            input.CreateSpace();
            input.Show(null,
                value: $"The renewals have now been imported into this new version " +
                "of the program. Nothing else will happen until new scheduled task is " +
                "first run *or* you trigger them manually. It is highly recommended " +
                $"to review the imported items with '{listCommand}' and to monitor the " +
                $"results of the first execution with '{renewCommand}'.");

        }
    }
}