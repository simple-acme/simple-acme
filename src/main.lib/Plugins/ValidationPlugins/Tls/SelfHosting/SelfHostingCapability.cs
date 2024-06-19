using PKISharp.WACS.Configuration;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Net.Sockets;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Tls
{
    internal class SelfHostingCapability : TlsValidationCapability
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

        internal Lazy<State> TestListener
        {
            get
            {
                return new(() =>
                {
                    var args = ArgumentsParser.GetArguments<SelfHostingArguments>();
                    var (testListener, port) = SelfHostingOptions is null ?
                        SelfHosting.CreateListener(args?.ValidationPort) :
                        SelfHosting.CreateListener(SelfHostingOptions.Port);
                    try
                    {
                        testListener.Start();
                        testListener.Stop();
                    }
                    catch (SocketException ex)
                    {
                        if (ex.SocketErrorCode == SocketError.AccessDenied)
                        {
                            return State.DisabledState($"Port {port} is in use by another program.");
                        }
                        else
                        {
                            return State.DisabledState(ex.Message);
                        }
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