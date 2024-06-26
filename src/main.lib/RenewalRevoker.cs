using PKISharp.WACS.Clients.Acme;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS
{
    internal class RenewalRevoker(
        ExceptionHandler exceptionHandler,
        ICacheService cacheService,
        IRenewalStore renewalStore,
        ILogService log,
        OrderManager orderManager,
        AcmeClientManager clientManager,
        DueDateStaticService dueDate,
        NotificationService notification) : IRenewalRevoker
    {

        /// <summary>
        /// Shared code for command line and renewal manager
        /// </summary>
        /// <param name="renewals"></param>
        /// <returns></returns>
        public async Task RevokeCertificates(IEnumerable<Renewal> renewals)
        {
            foreach (var renewal in renewals)
            {
                try
                {
                    log.Warning($"Revoke renewal {renewal.LastFriendlyName}");
                    var client = await clientManager.GetClient(renewal.Account);
                    var orders = dueDate.CurrentOrders(renewal);
                    var result = new RenewResult()
                    {
                        OrderResults = []
                    };
                    foreach (var order in orders.Where(x => !x.Revoked))
                    {
                        var cache = cacheService.PreviousInfo(renewal, order.Key);
                        if (cache != null)
                        {
                            try
                            {
                                var certificateDer = cache.Certificate.GetEncoded();
                                await client.RevokeCertificate(certificateDer);
                                result.OrderResults.Add(new OrderResult(order.Key) { Revoked = true });
                            }
                            catch (Exception ex)
                            {
                                result.OrderResults.Add(new OrderResult(order.Key)
                                {
                                    ErrorMessages = [$"Error revoking ({ex.Message})"]
                                });
                                log.Warning("Error revoking for {order}: {ex}", order, ex.Message);
                            }
                        }
                        else
                        {
                            log.Debug("No certificate found for {order}", order.Key);
                            result.OrderResults.Add(new OrderResult(order.Key)
                            {
                                ErrorMessages = [$"Error revoking (cert not found)"]
                            });
                        }
                    }

                    // Make sure private keys are not reused after this
                    cacheService.Revoke(renewal);
                    renewalStore.Save(renewal, result);
                }
                catch (Exception ex)
                {
                    exceptionHandler.HandleException(ex);
                }
            }

            // Delete order cache to prevent any chance of the
            // revoked certificates being reused on the a run
            orderManager.ClearCache();
        }

        /// <summary>
        /// Shared code for command line and renewal manager
        /// </summary>
        /// <param name="renewals"></param>
        /// <returns></returns>
        public async Task CancelRenewals(IEnumerable<Renewal> renewals)
        {
            foreach (var renewal in renewals)
            {
                log.Warning($"Cancelling renewal {renewal.LastFriendlyName}");
                try
                {
                    renewalStore.Cancel(renewal);
                    cacheService.Delete(renewal);
                    await notification.NotifyCancel(renewal);
                }
                catch (Exception ex)
                {
                    exceptionHandler.HandleException(ex);
                }
            }
        }
    }
}