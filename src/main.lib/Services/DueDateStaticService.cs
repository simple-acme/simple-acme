using PKISharp.WACS.DomainObjects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PKISharp.WACS.Services
{
    public class DueDateStaticService(
        DueDateRuntimeService runtime,
        ILogService logService)
    {
        public DueDate? DueDate(Renewal renewal)
        {
            var currentOrders = CurrentOrders(renewal);
            if (currentOrders.Any(x => x.Revoked))
            {
                return null;
            }
            return currentOrders.
                OrderBy(x => x.DueDate.Start).
                FirstOrDefault()?.
                DueDate;
        }

        public virtual bool IsDue(Renewal renewal)
        {
            var dueDate = DueDate(renewal);
            return dueDate == null || dueDate.Start < DateTime.Now;
        }

        private List<StaticOrderInfo> Mapping(Renewal renewal)
        {
            // Get most recent expire date for each order
            var expireMapping = new Dictionary<string, StaticOrderInfo>();
            foreach (var history in renewal.History.OrderBy(h => h.Date))
            {
                try
                {
                    var orderResults = history.OrderResults;
                    foreach (var orderResult in orderResults)
                    {
                        var info = default(StaticOrderInfo);
                        var key = orderResult.Name.ToLower();
                        var dueDate = orderResult.DueDate ??
                                runtime.ComputeDueDate(new DueDate()
                                {
                                    Start = history.Date,
                                    End = history.ExpireDate ?? history.Date.AddYears(1)
                                });

                        if (orderResult.Success != false)
                        {
                            if (!expireMapping.TryGetValue(key, out var value))
                            {
                                info = new StaticOrderInfo(key, dueDate);
                                expireMapping.Add(key, info);
                            }
                            else
                            {
                                info = value;
                                info.DueDate = dueDate;
                            }
                            if (orderResult.Success == true)
                            {
                                info.LastRenewal = history.Date;
                                info.RenewCount += 1;
                                info.LastThumbprint = orderResult.Thumbprint;
                            }
                        }
                        if (info != null)
                        {
                            info.Missing = orderResult.Missing == true;
                            info.Revoked = orderResult.Revoked == true;
                        }

                    }
                } 
                catch (Exception ex)
                {
                    logService.Error(ex, "Error reading history for {renewal}: {ex}", renewal.Id, ex.Message);
                }
            }
            return [.. expireMapping.Values];
        }

        public List<StaticOrderInfo> CurrentOrders(Renewal renewal) =>
            Mapping(renewal).
            Where(x => !x.Missing).
            ToList();
    }

    /// <summary>
    /// Information about a sub-order derived 
    /// and summarized from history entries
    /// </summary>
    public class StaticOrderInfo(string key, DueDate dueDate)
    {
        public string Key { get; set; } = key;
        public bool Missing { get; set; }
        public bool Revoked { get; set; }
        public DateTime? LastRenewal { get; set; }
        public string? LastThumbprint { get; set; }
        public int RenewCount { get; set; }
        public DueDate DueDate { get; set; } = dueDate;
    }
}
