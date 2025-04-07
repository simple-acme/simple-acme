using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services
{
    /// <summary>
    /// Manage the collection of renewals. The actual 
    /// implementations handle persistance of the objects
    /// </summary>
    internal class RenewalStore : IRenewalStore
    {
        internal ISettingsService _settings;
        internal ILogService _log;
        internal IInputService _inputService;
        internal DueDateStaticService _dueDateService;
        internal IRenewalStoreBackend _backend;

        public RenewalStore(
            IRenewalStoreBackend backend,
            ISettingsService settings,
            ILogService log,
            IInputService input,
            DueDateStaticService dueDateService)
        {
            _backend = backend;
            _log = log;
            _inputService = input;
            _settings = settings;
            _dueDateService = dueDateService;
            if (_settings.Valid)
            {
                _log.Debug("Renewal period: {RenewalDays} days", _settings.ScheduledTask.RenewalDays);
            }
        }

        public async Task<IEnumerable<Renewal>> FindByArguments(string? id, string? friendlyName)
        {
            // AND filtering by input parameters
            var ret = await _backend.Read();
            if (!string.IsNullOrEmpty(friendlyName))
            {
                var regex = new Regex(friendlyName.ToLower().PatternToRegex());
                ret = ret.Where(x => !string.IsNullOrEmpty(x.LastFriendlyName) && regex.IsMatch(x.LastFriendlyName.ToLower()));
            }
            if (!string.IsNullOrEmpty(id))
            {
                ret = ret.Where(x => string.Equals(id, x.Id, StringComparison.InvariantCultureIgnoreCase));
            }
            return ret;
        }

        public async Task Save(Renewal renewal, RenewResult result)
        {
            var renewals = await _backend.Read();
            var renewalList = renewals.ToList();
            if (renewal.New)
            {
                renewal.History = [];
                renewalList.Add(renewal);
                _log.Information(LogType.All, "Adding renewal for {friendlyName}", renewal.LastFriendlyName);
            }

            // Set next date
            renewal.History.Add(result);
            if (result.Success == true)
            {
                var date = _dueDateService.DueDate(renewal);
                if (date != null)
                {
                    _log.Information(LogType.All, "Next renewal due after {date}", _inputService.FormatDate(date.Start));
                }
            }
            renewal.Updated = true;
            await _backend.Write(renewalList);
        }

        /// <summary>
        /// Import from v1.9.x (obsolete)
        /// </summary>
        /// <param name="renewal"></param>
        /// <returns></returns>
        public async Task Import(Renewal renewal)
        {
            var renewals = await _backend.Read();
            var renewalList = renewals.ToList();
            renewalList.Add(renewal);
            _log.Information(LogType.All, "Importing renewal {renewal}", renewal);
            await _backend.Write(renewalList);
        }

        /// <summary>
        /// Re-save everything to apply new encrption setting
        /// </summary>
        /// <returns></returns>
        public async Task Encrypt()
        {
            _log.Information("Updating files in: {settings}", _settings.Client.ConfigurationPath);
            var renewals = await _backend.Read();
            foreach (var r in renewals)
            {
                r.Updated = true;
                _log.Information("Re-writing secrets for {renewal}", r);
            }
            await _backend.Write(renewals);
        }

        /// <summary>
        /// Get a list of current renewals
        /// </summary>
        public async Task<List<Renewal>> List() => [.. await _backend.Read()];

        /// <summary>
        /// Cancel specific renewal
        /// </summary>
        /// <param name="renewal"></param>
        public async Task Cancel(Renewal renewal)
        {
            var renewals = await _backend.Read();
            var found = renewals.FirstOrDefault(x => x.Id == renewal.Id);
            if (found == null)
            {
                _log.Warning("Renewal {renewal} not found", renewal.Id);
                return;
            }
            found.Deleted = true;
            await _backend.Write(renewals);
            _log.Warning("Renewal {renewal} cancelled", renewal);
        }

        /// <summary>
        /// Cancel everything
        /// </summary>
        public async Task Clear()
        {
            var renewals = await _backend.Read();
            _ = renewals.All(x => x.Deleted = true);
            await _backend.Write(renewals);
            _log.Warning("All renewals cancelled");
        }
    }

}