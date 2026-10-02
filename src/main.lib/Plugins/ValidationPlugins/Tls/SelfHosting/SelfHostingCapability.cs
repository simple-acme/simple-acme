using PKISharp.WACS.Configuration;
using PKISharp.WACS.DomainObjects;
using PKISharp.WACS.Plugins.Base.Capabilities;
using PKISharp.WACS.Plugins.Interfaces;
using PKISharp.WACS.Services;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.ValidationPlugins.Tls
{
    internal class SelfHostingCapability : TlsValidationCapability
    {
        protected readonly IUserRoleService UserRoleService;
        protected readonly SelfHostingOptions? SelfHostingOptions;
        protected readonly ArgumentsParser ArgumentsParser;
        protected readonly ISettings Settings;

        public SelfHostingCapability(Target target, IUserRoleService user, ArgumentsParser args, ISettings settings) : base(target)
        {
            UserRoleService = user;
            ArgumentsParser = args;
            Settings = settings;
        }

        public SelfHostingCapability(Target target, IUserRoleService user, ArgumentsParser args, ISettings settings, SelfHostingOptions? options) : base(target)
        {
            UserRoleService = user;
            SelfHostingOptions = options;
            ArgumentsParser = args;
            Settings = settings;
        }

        public override async Task<State> ExecutionState()
        {
            var baseState = await base.ExecutionState();
            if (baseState.Disabled)
            {
                return baseState;
            }
            return TestListener.Value;
        }

        internal Lazy<State> TestListener
        {
            get
            {
                return new(() =>
                {
                    var args = ArgumentsParser.GetArguments<SelfHostingArguments>();
                    var (testListener, port) = SelfHostingOptions is null ?
                        SelfHosting.CreateListener(args?.ValidationPort ?? Settings.Validation.ValidationPort) :
                        SelfHosting.CreateListener(SelfHostingOptions.Port ?? Settings.Validation.ValidationPort);
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
