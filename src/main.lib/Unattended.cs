using PKISharp.WACS.Clients.Acme;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS
{
    internal class Unattended(
        MainArguments args,
        IRenewalStore renewalStore,
        IInputService input,
        ILogService log,
        DueDateStaticService dueDate,
        IRenewalRevoker renewalRevoker,
        AccountArguments accountArguments,
        AcmeClientManager clientManager)
    {

        /// <summary>
        /// For command line --list
        /// </summary>
        /// <returns></returns>
        internal async Task List()
        {
            var renewals = await renewalStore.List();
            await input.WritePagedList(
                 renewals.Select(x => Choice.Create<Renewal?>(x,
                    description: x.ToString(dueDate, input),
                    color: x.History.Last().Success == true ?
                            dueDate.IsDue(x) ?
                                ConsoleColor.DarkYellow :
                                ConsoleColor.Green :
                            ConsoleColor.Red)));
        }

        /// <summary>
        /// Cancel certificate from the command line
        /// </summary>
        /// <returns></returns>
        internal async Task Cancel()
        {
            var targets = await FilterRenewalsByCommandLine("cancel");
            await renewalRevoker.CancelRenewals(targets);
        }

        /// <summary>
        /// Revoke certifcate from the command line
        /// </summary>
        /// <returns></returns>
        internal async Task Revoke()
        {
            log.Warning($"Certificates should only be revoked in case of a (suspected) security breach. Cancel the renewal if you simply don't need the certificate anymore.");
            var renewals = await FilterRenewalsByCommandLine("revoke");
            await renewalRevoker.RevokeCertificates(renewals);
        }

        /// <summary>
        /// Register new ACME account from the command line
        /// </summary>
        /// <returns></returns>
        internal async Task Register() => await clientManager.GetClient(accountArguments.Account);

        /// <summary>
        /// Filters for unattended mode
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        private async Task<IEnumerable<Renewal>> FilterRenewalsByCommandLine(string command)
        {
            if (args.HasFilter)
            {
                var targets = await renewalStore.FindByArguments(args.Id, args.FriendlyName);
                if (!targets.Any())
                {
                    log.Error("No renewals matched.");
                }
                return targets;
            }
            else
            {
                log.Error($"Specify which renewal to {command} using the parameter --id or --friendlyname.");
            }
            return [];
        }
    }
}