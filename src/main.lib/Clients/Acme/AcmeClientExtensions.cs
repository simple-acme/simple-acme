using ACMESharp.Protocol;
using ACMESharp.Protocol.Resources;
using PKISharp.WACS.Services;
using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PKISharp.WACS.Clients.Acme
{
    internal static class AcmeClientExtensions
    {
        /// <summary>
        /// Get a new nonce to use by the client
        /// </summary>
        /// <returns></returns>
        private static async Task GetNonce(this AcmeProtocolClient client, ILogService log) => await client.Backoff(async () => {
            await client.GetNonceAsync();
            return 1;
        }, log);

        /// <summary>
        /// According to the ACME standard, we SHOULD retry calls
        /// if there is an invalid nonce. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="executor"></param>
        /// <returns></returns>
        internal static async Task<T> Retry<T>(this AcmeProtocolClient client, Func<Task<T>> executor, ILogService log, int attempt = 0)
        {
            if (attempt == 0)
            {
                await _requestLock.WaitAsync();
            }
            try
            {
                return await client.Backoff(async () => {
                    if (string.IsNullOrEmpty(client.NextNonce))
                    {
                        await client.GetNonce(log);
                    }
                    return await executor();
                }, log);
            }
            catch (AcmeProtocolException apex)
            {
                if (attempt < 3 && apex.ProblemType == ProblemType.BadNonce)
                {
                    log.Warning("First chance error calling into ACME server, retrying with new nonce...");
                    await client.GetNonce(log);
                    return await client.Retry(executor, log, attempt + 1);
                }
                else if (apex.ProblemType == ProblemType.UserActionRequired)
                {
                    log.Error("{detail}: {instance}", apex.ProblemDetail, apex.ProblemInstance);
                    throw;
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                if (attempt == 0)
                {
                    _requestLock.Release();
                }
            }
        }

        /// <summary>
        /// Retry a call to the AcmeService up to five times, with a bigger
        /// delay for each time that the call fails with a TooManyRequests 
        /// response
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="executor"></param>
        /// <param name="attempt"></param>
        /// <returns></returns>
        internal static async Task<T> Backoff<T>(this AcmeProtocolClient client, Func<Task<T>> executor, ILogService log, int attempt = 0)
        {
            try
            {
                return await executor();
            }
            catch (Exception ex)
            {
                if (ex is AcmeProtocolException ape && 
                    ape.Response.StatusCode == HttpStatusCode.TooManyRequests && 
                    ape.ProblemType != ProblemType.RateLimited)
                {
                    return await Backoff(client, executor, log, attempt, "Server is too busy", ex);
                }
                if (ex is HttpRequestException hrex)
                {
                    if (hrex.StatusCode == null)
                    {
                        return await Backoff(client, executor, log, attempt, $"Request failed ({hrex.HttpRequestError})", ex);
                    }
                    if (RetryCodes.Contains(hrex.StatusCode.Value))
                    {
                        return await Backoff(client, executor, log, attempt, $"Error {(int)hrex.StatusCode.Value} ({hrex.StatusCode.Value})", ex);
                    }
                }
                throw;
            }
        }

        /// <summary>
        /// Inner method for Backoff that does the actual retrying with delay, and throws a more descriptive error message if the max attempts is reached
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="client"></param>
        /// <param name="executor"></param>
        /// <param name="log"></param>
        /// <param name="attempt"></param>
        /// <param name="errorMessage"></param>
        /// <param name="ex"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private static async Task<T> Backoff<T>(this AcmeProtocolClient client, Func<Task<T>> executor, ILogService log, int attempt, string errorMessage, Exception ex)
        {
            if (attempt == 5)
            {
                throw new Exception($"{errorMessage}, try again later", ex);
            }
            var delaySeconds = (int)Math.Pow(2, attempt + 3); // 5 retries with 8 to 128 seconds delay
            log.Warning(ex, $"{errorMessage}, backing off for {{n}} seconds...", delaySeconds);
            await Task.Delay(1000 * delaySeconds);
            return await client.Backoff(executor, log, attempt + 1);
        }

        /// <summary>
        /// List of Http Status codes that we should retry on, in case of transient network errors.
        /// </summary>
        private static readonly HttpStatusCode[] RetryCodes = [
            HttpStatusCode.RequestTimeout,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.GatewayTimeout
        ];

        /// <summary>
        /// Prevent sending simulateous requests to the ACME service because it messes
        /// up the nonce tracking mechanism
        /// </summary>
        private static readonly SemaphoreSlim _requestLock = new(1, 1);
    }
}
