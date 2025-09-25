using ACMESharp.Authorizations;
using PKISharp.WACS.Context;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using PKISharp.WACS.Services.Serialization;
using System;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    [IPlugin.Plugin1<
        SelfHostingOptions, SelfHostingOptionsFactory, 
        SelfHostingCapability, WacsJsonPlugins, SelfHostingArguments>
        ("c7d5e050-9363-4ba1-b3a8-931b31c618b7", 
        "SelfHosting", "Let simple-acme answer HTTP validation request", 
        Name = "Self-hosting")]
    internal class SelfHosting(ILogService log, RunLevel runLevel, IInputService input, ISelfHosterFactory factory, SelfHostingOptions options) : 
        HttpValidationBase(log, runLevel, input)
    {
        /// <summary>
        /// We can answer requests for multiple domains
        /// </summary>
        public override ParallelOperations Parallelism => ParallelOperations.Answer;

        private ISelfHoster Listener
        {
            get
            {
                _listener ??= factory.Create(options);
                return _listener;
            }
        }
        private ISelfHoster? _listener;

        public override async Task<bool> PrepareChallenge(ValidationContext context, Http01ChallengeValidationDetails challenge)
        {
            Listener.Challenges.Add(challenge.HttpResourceName, challenge.HttpResourceValue);
            if (!Listener.Started)
            {
                try
                {
                    await Listener.Start();
                }
                catch (Exception ex)
                {
                    log.Error(ex, "Unable to activate listener on port {port}", Listener.Port);
                    throw;
                }
            }
            await TestChallenge(challenge);
            return true;
        }

        public override Task Commit() => Task.CompletedTask;

        public override async Task CleanUp()
        {
            // Cleanup listener if nobody else has done it yet
            if (_listener != null)
            {
                try
                {
                    await _listener.Stop();
                }
                finally
                {
                    _listener = null;
                }
            }
        }
    }
}
