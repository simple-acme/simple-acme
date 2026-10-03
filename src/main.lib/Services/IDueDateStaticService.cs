using PKISharp.WACS.DomainObjects;
using System.Collections.Generic;

namespace PKISharp.WACS.Services
{
    public interface IDueDateStaticService
    {
        List<StaticOrderInfo> CurrentOrders(Renewal renewal);
        DueDate? DueDate(Renewal renewal);
        bool IsDue(Renewal renewal);
    }
}