﻿using Autofac;
using Autofac.Core;
using PKISharp.WACS.Clients.Acme;
using PKISharp.WACS.Clients.IIS;
using PKISharp.WACS.Configuration;
using PKISharp.WACS.Configuration.Arguments;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using PKISharp.WACS.Plugins.Base.Options;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.TargetPlugins;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PKISharp.WACS
{

    public enum Shortcuts
    {
        A, // List all selected
        C, // Cancel
        D, // Show details
        E, // Edit
        F, // Filter
        L, // Show command line
        O, // Sort
        Q, // Quit
        R, // Run
        S, // Run force
        T, // Run force, no cache
        U, // Analyze
        V, // Revoke
        X, // Reset filter and sort
    }

    internal class RenewalManager(
        ArgumentsParser arguments, MainArguments args,
        IRenewalStore renewalStore, ISharingLifetimeScope container,
        IInputService input, ILogService log,
        ISettings settings, DueDateStaticService dueDate,
        IAutofacBuilder autofacBuilder, ExceptionHandler exceptionHandler,
        RenewalCreator renewalCreator, RenewalExecutor renewalExecutor,
        AccountManager accountManager, RenewalDescriber renewalDescriber,
        IRenewalRevoker renewalRevoker, AcmeClientManager acmeClient)
    {

        /// <summary>
        /// Renewal management mode
        /// </summary>
        /// <returns></returns>
        internal async Task ManageRenewals()
        {
            IEnumerable<Renewal> originalSelection = (await renewalStore.List()).OrderBy(x => x.LastFriendlyName);
            var selectedRenewals = originalSelection;
            var quit = false;
            var displayAll = false;
            do
            {
                var all = selectedRenewals.Count() == originalSelection.Count();
                var none = !selectedRenewals.Any();
                var totalLabel = originalSelection.Count() != 1 ? "renewals" : "renewal";
                var renewalSelectedLabel = selectedRenewals.Count() != 1 ? "renewals" : "renewal";
                var selectionLabel = 
                    all ? selectedRenewals.Count() == 1 ? "the renewal" : "*all* renewals" : 
                    none ? "no renewals" :  
                    $"{selectedRenewals.Count()} of {originalSelection.Count()} {totalLabel}";

                input.CreateSpace();
                input.Show(null, 
                    "Welcome to the renewal manager. Actions selected in the menu below will " +
                    "be applied to the following list of renewals. You may filter the list to target " +
                    "your action at a more specific set of renewals, or sort it to make it easier to " +
                    "find what you're looking for.");

                var displayRenewals = selectedRenewals;
                var displayLimited = !displayAll && selectedRenewals.Count() >= settings.UI.PageSize;
                var displayHidden = 0;
                var displayHiddenLabel = "";
                if (displayLimited)
                {
                    displayRenewals = displayRenewals.Take(settings.UI.PageSize - 1);
                    displayHidden = selectedRenewals.Count() - displayRenewals.Count();
                    displayHiddenLabel = displayHidden != 1 ? "renewals" : "renewal";
                }
                var choices = displayRenewals.Select(x => Choice.Create<Renewal?>(x,
                                  description: x.ToString(dueDate, input),
                                  color: x.History.LastOrDefault()?.Success ?? true ?
                                          dueDate.IsDue(x) ?
                                              ConsoleColor.DarkYellow :
                                              ConsoleColor.Green :
                                          ConsoleColor.Red)).ToList();
                if (displayLimited)
                {
                    choices.Add(Choice.Create<Renewal?>(null,
                                  command: "More",
                                  description: $"{displayHidden} additional {displayHiddenLabel} selected but currently not displayed"));
                }
                await input.WritePagedList(choices);
                displayAll = false;

                var noneState = none ? State.DisabledState("No renewals selected.") : State.EnabledState();
                var sortFilterState = selectedRenewals.Count() < 2 ? State.DisabledState("Not enough renewals to sort/filter.") : State.EnabledState();
                var editState =
                    selectedRenewals.Count() != 1 
                        ? none 
                            ? State.DisabledState("No renewals selected.") 
                            : State.DisabledState("Multiple renewals selected.") 
                        : State.EnabledState();

                var options = new List<Choice<Func<Task>>>();
                if (displayLimited)
                {
                    options.Add(
                        Choice.Create(
                            () => { displayAll = true; return Task.CompletedTask; },
                            "List all selected renewals", Shortcuts.A.ToString()));
                }
                options.Add(
                    Choice.Create(
                        async () => { quit = true; await EditRenewal(selectedRenewals.First()); },
                        "Edit renewal", Shortcuts.E.ToString(), state: editState));
                if (selectedRenewals.Count() > 1)
                {
                    options.Add(
                        Choice.Create<Func<Task>>(
                            async () => selectedRenewals = await FilterRenewalsMenu(selectedRenewals),
                            all ? "Apply filter" : "Apply additional filter", Shortcuts.F.ToString(), state: sortFilterState));
                    options.Add(
                        Choice.Create<Func<Task>>(
                             async () => selectedRenewals = await SortRenewalsMenu(selectedRenewals),
                            "Sort renewals", Shortcuts.O.ToString(), state: sortFilterState));
                }
                if (!all)
                {
                    options.Add(
                        Choice.Create(
                            () => { selectedRenewals = originalSelection; return Task.CompletedTask; },
                            "Reset sorting and filtering", Shortcuts.X.ToString()));
                }
                options.Add(
                    Choice.Create(
                        async () => { 
                            foreach (var renewal in selectedRenewals) {
                                var index = selectedRenewals.ToList().IndexOf(renewal) + 1;
                                input.Show($"-");
                                input.Show($"Renewal {index}/{selectedRenewals.Count()}");
                                input.Show($"-");
                                renewalDescriber.Show(renewal);
                                var cont = false;
                                if (index != selectedRenewals.Count())
                                {
                                    cont = await input.Wait("Press <Enter> to continue or <Esc> to abort");
                                    if (!cont)
                                    {
                                        break;
                                    }
                                } 
                                else
                                {
                                    await input.Wait();
                                }

                            } 
                        },
                        $"Show details for {selectionLabel}", Shortcuts.D.ToString(),
                        state: noneState));
                options.Add(
                    Choice.Create(
                        async () => {
                            foreach (var renewal in selectedRenewals)
                            {
                                input.Show(null, renewalDescriber.Describe(renewal));
                                input.CreateSpace();
                            }
                            await input.Wait();
                        },
                        $"Show command line for {selectionLabel}", Shortcuts.L.ToString(),
                        state: noneState));
                options.Add(
                    Choice.Create(() => Run(selectedRenewals, RunLevel.Interactive),
                        $"Run {selectionLabel}", Shortcuts.R.ToString(), state: noneState));
                options.Add(
                    Choice.Create(() => Run(selectedRenewals, RunLevel.Interactive | RunLevel.Force),
                        $"Run {selectionLabel} (force)", Shortcuts.S.ToString(), state: noneState));
                if (settings.Cache.ReuseDays > 0)
                {
                    options.Add(
                        Choice.Create(() => Run(selectedRenewals, RunLevel.Interactive | RunLevel.Force | RunLevel.NoCache),
                        $"Run {selectionLabel} (force, no cache)", Shortcuts.T.ToString(), state: noneState));
                }
                options.Add(
                    Choice.Create<Func<Task>>(
                        async () => selectedRenewals = await Analyze(selectedRenewals),
                        $"Analyze duplicates for {selectionLabel}", Shortcuts.U.ToString(), state: noneState));
                options.Add(
                    Choice.Create<Func<Task>>(
                        async () => {
                            var confirm = await input.PromptYesNo($"Are you sure you want to cancel {selectedRenewals.Count()} currently selected {renewalSelectedLabel}?", false);
                            if (confirm)
                            {
                                await renewalRevoker.CancelRenewals(selectedRenewals);
                                var list = await renewalStore.List();
                                originalSelection = list.OrderBy(x => x.LastFriendlyName);
                                selectedRenewals = originalSelection;
                            }
                        },
                        $"Cancel {selectionLabel}", Shortcuts.C.ToString(), state: noneState));
                options.Add(
                    Choice.Create(
                        async () => {
                            var confirm = await input.PromptYesNo($"Are you sure you want to revoke the most recently issued certificate for {selectedRenewals.Count()} currently selected {renewalSelectedLabel}? This should only be done in case of a (suspected) security breach. Cancel the {renewalSelectedLabel} if you simply don't need the certificates anymore.", false);
                            if (confirm)
                            {
                                await renewalRevoker.RevokeCertificates(selectedRenewals);
                            }
                        },
                        $"Revoke certificate(s) for {selectionLabel}", Shortcuts.V.ToString(), state: noneState));
                options.Add(
                    Choice.Create(
                        () => { quit = true; return Task.CompletedTask; },
                        "Back", Shortcuts.Q.ToString(),
                        @default: !originalSelection.Any()));

                if (selectedRenewals.Count() > 1)
                {
                    input.CreateSpace();
                    input.Show(null, $"Currently selected {selectedRenewals.Count()} of {originalSelection.Count()} {totalLabel}");
                }
                var chosen = await input.ChooseFromMenu(
                    "Choose an action or type numbers to select renewals",
                    options, 
                    (string unexpected) =>
                        Choice.Create(() => { selectedRenewals = FilterRenewalsById(selectedRenewals, unexpected); return Task.CompletedTask; }));
                await chosen.Invoke();
                container.Resolve<IIISClient>().Refresh();
            }
            while (!quit);
        }

        /// <summary>
        /// Run selected renewals
        /// </summary>
        /// <param name="selectedRenewals"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        private async Task Run(IEnumerable<Renewal> selectedRenewals, RunLevel flags)
        {
            WarnAboutRenewalArguments();
            foreach (var renewal in selectedRenewals)
            {
                await ProcessRenewal(renewal, flags);
            }
        }

        /// <summary>
        /// Helper to get target for a renewal
        /// </summary>
        /// <param name="renewal"></param>
        /// <returns></returns>
        private async Task<Target?> GetTarget(Renewal renewal)
        {
            try
            {
                using var targetScope = autofacBuilder.PluginBackend<ITargetPlugin, TargetPluginOptions>(container, renewal.TargetPluginOptions);
                var targetBackend = targetScope.Resolve<ITargetPlugin>();
                return await targetBackend.Generate();
            } 
            catch
            {
                
            }
            return null;
        }

        /// <summary>
        /// Check if there are multiple renewals installing to the same site 
        /// or requesting certificates for the same domains
        /// </summary>
        /// <param name="selectedRenewals"></param>
        /// <returns></returns>
        private async Task<IEnumerable<Renewal>> Analyze(IEnumerable<Renewal> selectedRenewals)
        {
            var foundHosts = new Dictionary<Identifier, List<Renewal>>();
            var foundSites = new Dictionary<long, List<Renewal>>();

            foreach (var renewal in selectedRenewals)
            {
                var initialTarget = await GetTarget(renewal);
                if (initialTarget == null)
                {
                    log.Warning("Unable to generate source for renewal {renewal}, analysis incomplete", renewal.FriendlyName);
                    continue;
                }
                foreach (var targetPart in initialTarget.Parts)
                {
                    if (targetPart.SiteId != null)
                    {
                        var siteId = targetPart.SiteId.Value;
                        if (!foundSites.TryGetValue(siteId, out var value))
                        {
                            value = [];
                            foundSites.Add(siteId, value);
                        }
                        value.Add(renewal);
                    }
                    foreach (var host in targetPart.GetIdentifiers(true))
                    {
                        if (!foundHosts.TryGetValue(host, out var value))
                        {
                            value = [];
                            foundHosts.Add(host, value);
                        }
                        value.Add(renewal);
                    }
                }
            }

            // List results
            var options = new List<Choice<List<Renewal>>>();
            foreach (var site in foundSites)
            {
                if (site.Value.Count > 1)
                {
                    options.Add(
                      Choice.Create(
                          site.Value,
                          $"Select {site.Value.Count} renewals covering IIS site {site.Key}"));
                }
            }
            foreach (var host in foundHosts)
            {
                if (host.Value.Count > 1)
                {
                    options.Add(
                      Choice.Create(
                          host.Value,
                          $"Select {host.Value.Count} renewals covering host {host.Key.Value}"));
                }
            }
            input.CreateSpace();
            if (options.Count == 0)
            {
                input.Show(null, "Analysis didn't find any overlap between renewals.");
                return selectedRenewals;
            }
            else
            {
                options.Add(
                    Choice.Create(
                        selectedRenewals.ToList(),
                        $"Back"));
                input.Show(null, "Analysis found some overlap between renewals. You can select the overlapping renewals from the menu.");
                return await input.ChooseFromMenu("Please choose from the menu", options);
            }
        }

        /// <summary>
        /// Offer user different ways to sort the renewals
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        private async Task<IEnumerable<Renewal>> SortRenewalsMenu(IEnumerable<Renewal> current)
        {
            var options = new List<Choice<Func<IEnumerable<Renewal>>>>
            {
                Choice.Create<Func<IEnumerable<Renewal>>>(
                    () => current.OrderBy(x => x.LastFriendlyName ?? ""),
                    "Sort by friendly name",
                    @default: true),
                Choice.Create<Func<IEnumerable<Renewal>>>(
                    () => current.OrderByDescending(x => x.LastFriendlyName ?? ""),
                    "Sort by friendly name (descending)"),
                Choice.Create<Func<IEnumerable<Renewal>>>(
                    () => current.OrderBy(x => dueDate.DueDate(x)),
                    "Sort by due date"),
                Choice.Create<Func<IEnumerable<Renewal>>>(
                    () => current.OrderByDescending(x => dueDate.DueDate(x)),
                    "Sort by due date (descending)")
            };
            var chosen = await input.ChooseFromMenu("How would you like to sort the renewals list?", options);
            return chosen.Invoke();
        }

        /// <summary>
        /// Offer user different ways to filter the renewals
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        private async Task<IEnumerable<Renewal>> FilterRenewalsMenu(IEnumerable<Renewal> current)
        {
            var options = new List<Choice<Func<Task<IEnumerable<Renewal>>>>>
            {
                Choice.Create<Func<Task<IEnumerable<Renewal>>>>(
                    () => FilterRenewalsByFriendlyName(current),
                    "Filter by friendly name"),
                Choice.Create<Func<Task<IEnumerable<Renewal>>>>(
                    () => Task.FromResult(current.Where(x => dueDate.IsDue(x))),
                    "Filter by due status (keep due)"),
                Choice.Create<Func<Task<IEnumerable<Renewal>>>>(
                    () => Task.FromResult(current.Where(x => !dueDate.IsDue(x))),
                    "Filter by due status (remove due)"),
                Choice.Create<Func<Task<IEnumerable<Renewal>>>>(
                    () => Task.FromResult(current.Where(x => x.History.Last().Success != true)),
                    "Filter by error status (keep errors)"),
                Choice.Create<Func<Task<IEnumerable<Renewal>>>>(
                    () => Task.FromResult(current.Where(x => x.History.Last().Success == true)),
                    "Filter by error status (remove errors)"),
                Choice.Create<Func<Task<IEnumerable<Renewal>>>>(
                    () => Task.FromResult(current),
                    "Cancel")
            };
            var chosen = await input.ChooseFromMenu("How would you like to filter?", options);
            return await chosen.Invoke();
        }

        private IEnumerable<Renewal> FilterRenewalsById(IEnumerable<Renewal> current, string input)
        {
            var parts = input.ParseCsv();
            if (parts == null)
            {
                return current;
            }
            var ret = new List<Renewal>();
            foreach (var part in parts)
            {
                if (int.TryParse(part, out var index))
                {
                    if (index > 0 && index <= current.Count())
                    {
                        ret.Add(current.ElementAt(index - 1));
                    }
                    else
                    {
                        log.Warning("Input out of range: {part}", part);
                    }
                }
                else
                {
                    log.Warning("Invalid input: {part}", part);
                }
            }
            return ret;
        }

        /// <summary>
        /// Filter specific renewals by friendly name
        /// </summary>
        /// <param name="current"></param>
        /// <returns></returns>
        private async Task<IEnumerable<Renewal>> FilterRenewalsByFriendlyName(IEnumerable<Renewal> current)
        {
            input.CreateSpace();
            input.Show(null, "Please input friendly name to filter renewals by. " + IISArguments.PatternExamples);
            var rawInput = await input.RequestString("Friendly name");
            var ret = new List<Renewal>();
            var regex = new Regex(rawInput.PatternToRegex(), RegexOptions.IgnoreCase);
            foreach (var r in current)
            {
                if (!string.IsNullOrEmpty(r.LastFriendlyName) && regex.Match(r.LastFriendlyName).Success)
                {
                    ret.Add(r);
                }
            }
            return ret;
        }

        /// <summary>
        /// Loop through the store renewals and run those which are
        /// due to be run
        /// </summary>
        internal async Task CheckRenewals(RunLevel runLevel)
        {
            IEnumerable<Renewal> renewals;
            if (args.HasFilter)
            {
                renewals = await renewalStore.FindByArguments(args.Id, args.FriendlyName);
                if (!renewals.Any())
                {
                    log.Error("No renewals found that match the filter parameters --id and/or --friendlyname.");
                }
            }
            else
            {
                log.Verbose("Checking renewals");
                renewals = await renewalStore.List();
                if (!renewals.Any())
                {
                    log.Warning("No scheduled renewals found.");
                }
            }

            if (renewals.Any())
            {
                WarnAboutRenewalArguments();
                foreach (var renewal in renewals)
                {
                    try
                    {
                        var success = await ProcessRenewal(renewal, runLevel);
                        if (success == false)
                        {
                            // Make sure the ExitCode is set
                            exceptionHandler.HandleException();
                        }
                    } 
                    catch (Exception ex)
                    {
                        exceptionHandler.HandleException(ex, "Unhandled error processing renewal");
                        continue;
                    }
                }
            }
        }

        /// <summary>
        /// Process a single renewal
        /// </summary>
        /// <param name="renewal"></param>
        internal async Task<bool?> ProcessRenewal(Renewal renewal, RunLevel runLevel)
        {
            var notification = container.Resolve<NotificationService>();
            try
            {
                var result = await renewalExecutor.HandleRenewal(renewal, runLevel);
                if (result.OrderResults.Count != 0)
                {
                    await renewalStore.Save(renewal, result);
                }
                if (!result.Abort)
                {
                    if (result.Success == true)
                    {
                        await notification.NotifySuccess(renewal, log.Lines);
                        return true;
                    }
                    else
                    {
                        await notification.NotifyFailure(runLevel, renewal, result, log.Lines);
                        return false;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                exceptionHandler.HandleException(ex);
                await notification.NotifyFailure(runLevel, renewal, new RenewResult(ex.Message), log.Lines);
                return false;
            }
        }

        /// <summary>
        /// Show a warning when the user appears to be trying to
        /// use command line arguments in combination with a renew
        /// command.
        /// </summary>
        internal void WarnAboutRenewalArguments()
        {
            if (arguments.Active())
            {
                log.Warning("You have specified command line options for plugins. " +
                    "Note that these only affect new certificates, but NOT existing renewals. " +
                    "To change settings, re-create (overwrite) the renewal.");
            }
        }

        /// <summary>
        /// "Edit" renewal
        /// </summary>
        private async Task EditRenewal(Renewal renewal)
        {
            var certProfileCount = (await acmeClient.GetMetaData())?.Profiles?.Keys.Count;
            var options = new List<Choice<Steps>>
            {
                Choice.Create(Steps.All, "All"),
                Choice.Create(Steps.Source, "Source"),
                Choice.Create(Steps.Order, "Order"),
                Choice.Create(Steps.Csr, "CSR"),
                Choice.Create(Steps.Validation, "Validation"),
                Choice.Create(Steps.Store, "Store"),
                Choice.Create(Steps.Installation, "Installation"),
                Choice.Create(Steps.Account, "Account", state: accountManager.ListAccounts().Count() > 1 ? State.EnabledState() : State.DisabledState("Only one account is registered.")),
                Choice.Create(Steps.Profile, "Certificate profile", state: 
                    certProfileCount > 1 ? State.EnabledState() : 
                    certProfileCount == 0 ? State.DisabledState("Certificate profiles not supported.") : 
                    State.DisabledState("Only one certificate profile available.")),
                Choice.Create(Steps.None, "Cancel")
            };
            var chosen = await input.ChooseFromMenu("Which step do you want to edit?", options);
            if (chosen != Steps.None)
            {
                await renewalCreator.SetupRenewal(RunLevel.Interactive | RunLevel.Advanced | RunLevel.Force | RunLevel.NoTaskScheduler, chosen, renewal);
            }
        }
    }
}
