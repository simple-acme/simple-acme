using PKISharp.WACS.Plugins.Interfaces;
using System.Threading.Tasks;

namespace PKISharp.WACS.Plugins.Base.Capabilities
{
    public class DefaultCapability : IPluginCapability
    {
        public virtual Task<State> ExecutionState() => Task.FromResult(State.EnabledState());

        public virtual Task<State> ConfigurationState() => ExecutionState();
    }
}
