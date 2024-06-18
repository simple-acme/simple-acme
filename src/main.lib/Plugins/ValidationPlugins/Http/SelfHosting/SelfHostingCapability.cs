using PKISharp.WACS.Configuration;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Plugins.ValidationPlugins.Http;
using PKISharp.WACS.Services;
using System;
using System.Net;

namespace PKISharp.WACS.Plugins.ValidationPlugins
{
    internal class SelfHostingCapability : HttpValidationCapability
    {
        protected readonly IUserRoleService UserRoleService;
        protected readonly SelfHostingOptions? SelfHostingOptions;
        protected readonly ArgumentsParser ArgumentsParser;

        public SelfHostingCapability(Target target, IUserRoleService user, ArgumentsParser args) : base(target)
        {
            UserRoleService = user;
            ArgumentsParser = args;
        }

        public SelfHostingCapability(Target target, IUserRoleService user, ArgumentsParser args, SelfHostingOptions? options) : base(target) 
        {
            UserRoleService = user;
            SelfHostingOptions = options;
            ArgumentsParser = args;
        } 

        public override State State
        {
            get
            {
                var baseState = base.State;
                if (baseState.Disabled)
                {
                    return baseState;
                }
                return TestListener.Value;
            }
        }

        public static (HttpListener, int) CreateFromArgs(SelfHostingArguments? args) =>
            SelfHosting.CreateFromOptions(new SelfHostingOptions() { 
                Https = args?.ValidationProtocol?.ToLower() == "https",
                Port = args?.ValidationPort 
            });

        internal Lazy<State> TestListener
        {
            get
            {
                return new(() =>
                {
                    var args = ArgumentsParser.GetArguments<SelfHostingArguments>();
                    var (testListener, port) = SelfHostingOptions is null ?
                        CreateFromArgs(args) :
                        SelfHosting.CreateFromOptions(SelfHostingOptions);
                    try
                    {
                        testListener.Start();
                        testListener.Stop();
                    }
                    catch (HttpListenerException hex)
                    {
                        if (hex.ErrorCode == 5)
                        {
                            return State.DisabledState("Run as administrator to allow opening a HTTP listener.");
                        }
                        else if (hex.ErrorCode == 32)
                        {
                            return State.DisabledState($"Another program appears to be using port {port}.");
                        }
                        return State.DisabledState(hex.Message);
                    }
                    catch (Exception ex)
                    {
                        return State.DisabledState(ex.Message);
                    }
                    return State.EnabledState();
                });
            }
        }
    }
}
