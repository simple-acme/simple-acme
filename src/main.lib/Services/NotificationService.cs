using Autofac;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Services.Interfaces;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    internal class NotificationService(
        ILifetimeScope scope,
        ILogService log,
        IPluginService pluginService)
    {
        private readonly ILogService _log = log;
        private readonly IEnumerable<INotificationTarget> _targets = [.. pluginService.
                GetNotificationTargets().
                Select(b => scope.Resolve(b.Backend)).
                OfType<INotificationTarget>()];
        private IEnumerable<INotificationTarget> EnabledTargets => _targets.Where(t => !t.State.Disabled);

        /// <summary>
        /// Handle created notification
        /// </summary>
        /// <param name="runLevel"></param>
        /// <param name="renewal"></param>
        internal async Task NotifyCreated(Renewal renewal, IEnumerable<MemoryEntry> log)
        {
            _log.Information(
                LogType.All, 
                "Certificate {friendlyName} created", 
                renewal.LastFriendlyName);
            foreach (var target in EnabledTargets.Where(t => t.NotifyOnSuccess))
            {
                try
                {
                    await target.SendCreated(renewal, log);
                } 
                catch (Exception ex)
                {
                    _log.Error(ex, "Unable to send notification using {n}", target.Label);
                }
            }
        }

        /// <summary>
        /// Handle success notification
        /// </summary>
        /// <param name="runLevel"></param>
        /// <param name="renewal"></param>
        internal async Task NotifySuccess(Renewal renewal, IEnumerable<MemoryEntry> log)
        {
            var withErrors = log.Any(l => l.Level == LogEventLevel.Error);
            _log.Information(
                LogType.All, 
                "Renewal for {friendlyName} succeeded" + (withErrors ? " with errors" : ""),
                renewal.LastFriendlyName);
            foreach (var target in EnabledTargets.Where(t => withErrors || t.NotifyOnSuccess))
            {
                try
                {
                    await target.SendSuccess(renewal, log);
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Unable to send notification using {n}", target.Label);
                }
            }
        }

        /// <summary>
        /// Handle failure notification
        /// </summary>
        /// <param name="runLevel"></param>
        /// <param name="renewal"></param>
        internal async Task NotifyFailure(
            RunLevel runLevel, 
            Renewal renewal, 
            RenewResult result,
            IEnumerable<MemoryEntry> log)
        {
            _log.Error("Renewal for {friendlyName} failed, will retry on next run", renewal.LastFriendlyName);
            var errors = result.ErrorMessages?.ToList() ?? [];
            errors.AddRange(result.OrderResults?.SelectMany(o => o.ErrorMessages ?? Enumerable.Empty<string>()) ?? []);
            if (errors.Count == 0)
            {
                errors.Add("No specific error reason provided.");
            }
            errors.ForEach(e => _log.Error(e));
            
            // Do not send emails when running interactively      
            if (runLevel.HasFlag(RunLevel.Unattended))
            {
                foreach (var target in EnabledTargets)
                {
                    try
                    {
                        await target.SendFailure(renewal, log, errors);
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, "Unable to send notification using {n}", target.Label);
                    }
                }
            }
        }

        /// <summary>
        /// Handle failure notification
        /// </summary>
        /// <param name="runLevel"></param>
        /// <param name="renewal"></param>
        internal async Task NotifyTest()
        {
            foreach (var target in _targets)
            {
                if (target.State.Disabled)
                {
                    _log.Information("{n} disabled: {m}", target.Label, target.State.Reason);
                } 
                else
                {
                    try
                    {
                        _log.Information("Sending test notification via {n}", target.Label);
                        await target.SendTest();
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex, "Unable to send notification using {n}", target.Label);
                    }
                }
            }
        }

        internal async Task NotifyCancel(Renewal renewal) 
        {
            foreach (var target in EnabledTargets)
            {
                try
                {
                    await target.SendCancel(renewal);
                }
                catch
                {
                    _log.Error("Unable to send notification using {n}", target.Label);
                }
            }
        }
    }
}