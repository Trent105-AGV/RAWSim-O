using RAWSimO.Core.Configurations;
using RAWSimO.MultiAgentPathFinding;
using RAWSimO.MultiAgentPathFinding.Methods;

namespace RAWSimO.Core.Control.Defaults.PathPlanning;

/// <summary>
/// Controller of the bot.
/// </summary>
public class PASPathManager : PathManager
{

    /// <summary>
    /// constructor
    /// </summary>
    /// <param name="instance">instance</param>
    public PASPathManager(Instance instance)
        : base(instance)
    {

        //translate to lightweight graph
        var graph = GenerateGraph();
        var config = instance.ControllerConfig.PathPlanningConfig as PASPathPlanningConfiguration;

        PathFinder = new PASMethod(graph, instance.SettingConfig.Seed, new PathPlanningCommunicator(
            instance.LogSevere,
            instance.LogDefault,
            instance.LogInfo,
            instance.LogVerbose,
            () => { instance.StatOverallPathPlanningTimeouts++; }));
        var method = PathFinder as PASMethod;
        method.LengthOfAWaitStep = config.LengthOfAWaitStep;
        method.RuntimeLimitPerAgent = config.RuntimeLimitPerAgent;
        method.RunTimeLimitOverall = config.RunTimeLimitOverall;
        method.MaxPriorities = config.MaxPriorities;
        method.LengthOfAWindow = config.LengthOfAWindow;

    }
}