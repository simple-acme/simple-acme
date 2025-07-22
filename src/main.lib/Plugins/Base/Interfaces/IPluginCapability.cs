using System;
using System.Collections.Generic;

namespace PKISharp.WACS.Plugins.Interfaces
{
    public interface IPluginCapability
    {
        /// <summary>
        /// Indicates whether the plugin can be configured in the current context.
        /// This is a more relaxed version of ExecutionState, enabling users to 
        /// setup renewals that cannot be executed yet, but will be runnable by
        /// the scheduled task (with more access rights) or when the pre execution 
        /// scripts has run (e.g. to free port 80).
        /// </summary>
        /// <returns></returns>
        State ConfigurationState { get; }

        /// <summary>
        /// Indicates whether the plugin can run in the current context.
        /// </summary>
        /// <returns></returns>
        State ExecutionState { get; }
    }

    /// <summary>
    /// Handles installation
    /// </summary>
    public interface IInstallationPluginCapability : IPluginCapability
    {
        /// <summary>
        /// Can this plugin be selected given the current other selections.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        State CanInstall(IEnumerable<Type> storeTypes, IEnumerable<Type> installationTypes);
    }

    /// <summary>
    /// Handles validation
    /// </summary>
    public interface IValidationPluginCapability : IPluginCapability
    {
        /// <summary>
        /// Which type(s) of challenge can this plugin handle
        /// </summary>
        IEnumerable<string> ChallengeTypes { get; }
    }
}
