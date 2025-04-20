using ACMESharp.Authorizations;
using ACMESharp.Protocol.Resources;
using PKISharp.WACS.Context;
using PKISharp.WACS.Plugins.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    /// <summary>
    /// Base implementation for all validation plugins
    /// </summary>
    public abstract class Validation<TChallenge> : IValidationPlugin where TChallenge : IChallengeValidationDetails
    {
        /// <summary>
        /// Select one of the available challenges to process.
        /// This is only called when multiple challenges of 
        /// the supported type(s) are available.
        /// </summary>
        /// <param name="supportedChallenges"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public virtual Task<AcmeChallenge?> SelectChallenge(List<AcmeChallenge> supportedChallenges) => 
            Task.FromResult(supportedChallenges.FirstOrDefault());

        /// <summary>
        /// Handle the challenge
        /// </summary>
        /// <param name="challenge"></param>
        public async Task<bool> PrepareChallenge(ValidationContext context)
        {
            if (context.ChallengeDetails is TChallenge typed)
            {
                return await PrepareChallenge(context, typed);
            } 
            else
            {
                throw new InvalidOperationException("Unexpected challenge type");
            }
        }

        /// <summary>
        /// Handle the challenge
        /// </summary>
        /// <param name="challenge"></param>
        public abstract Task<bool> PrepareChallenge(ValidationContext context, TChallenge typed);

        /// <summary>
        /// Commit changes
        /// </summary>
        /// <returns></returns>
        public abstract Task Commit();

        /// <summary>
        /// Cleanup any changes made during PrepareChallenge and/or Commit
        /// </summary>
        /// <returns></returns>
        public abstract Task CleanUp();

        /// <summary>
        /// No parallelism by default
        /// </summary>
        public virtual ParallelOperations Parallelism => ParallelOperations.None;
    }
}
