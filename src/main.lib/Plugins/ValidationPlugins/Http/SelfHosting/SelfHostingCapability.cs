using PKISharp.WACS.Configuration;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using System;
using System.Net;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Http
{
    internal class SelfHostingCapability(Target target, ArgumentsParser args, ISelfHosterFactory factory) : HttpValidationCapability(target)
    {
        protected readonly ISelfHosterFactory Factory = factory;
        protected readonly SelfHostingOptions? SelfHostingOptions;
        protected readonly ArgumentsParser ArgumentsParser = args;

        public SelfHostingCapability(Target target, ArgumentsParser args, ISelfHosterFactory factory, SelfHostingOptions? options) : this(target, args, factory) => SelfHostingOptions = options;

        public override State ExecutionState
        {
            get
            {
                var baseState = base.ExecutionState;
                if (baseState.Disabled)
                {
                    return baseState;
                }
                return TestListener.Value;
            }
        }

        internal Lazy<State> TestListener
        {
            get
            {
                _testListener ??= new Lazy<State>(() => TestListenerCreator().Result);
                return _testListener;
            }
        }
        internal Lazy<State>? _testListener;

        internal async Task<State> TestListenerCreator() 
        {
            var args = ArgumentsParser.GetArguments<SelfHostingArguments>();
            var options = SelfHostingOptions as ISelfHosterOptions ?? args ?? new SelfHostingArguments();
            var listener = Factory.Create(options);
            try
            {
                await listener.Start();
                await listener.Stop();
            }
            catch (HttpListenerException hex)
            {
                if (hex.ErrorCode == 5)
                {
                    return State.DisabledState("Run as administrator to allow opening a HTTP listener.");
                }
                else if (hex.ErrorCode == 32)
                {
                    return State.DisabledState($"Another program appears to be using port {listener.Port}.");
                }
                return State.DisabledState(hex.Message);
            }
            catch (Exception ex)
            {
                return State.DisabledState(ex.Message);
            }
            return State.EnabledState();
        }
    }
}
