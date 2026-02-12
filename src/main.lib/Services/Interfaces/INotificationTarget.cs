using PKISharp.WACS.DomainObjects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PKISharp.WACS.Services.Interfaces
{
    internal interface INotificationTarget
    {
        bool Enabled { get; }
        bool NotifyOnSuccess { get; }
        internal Task SendCreated(Renewal renewal, IEnumerable<MemoryEntry> log);
        internal Task SendSuccess(Renewal renewal, IEnumerable<MemoryEntry> log);
        internal Task SendFailure(Renewal renewal, IEnumerable<MemoryEntry> log, IEnumerable<string> errors);
        internal Task SendCancel(Renewal renewal);
        internal Task SendTest();
    }
}
