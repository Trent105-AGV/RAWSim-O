using RAWSimO.Core.Bots;
using RAWSimO.Core.Configurations;
using RAWSimO.MultiAgentPathFinding;
using RAWSimO.MultiAgentPathFinding.Methods;

namespace RAWSimO.Core.Control.Defaults.PathPlanning;

/// <summary>
/// Controller of the bot.
/// </summary>
public class BCPPathManager : PathManager
{

    /// <summary>
    /// constructor
    /// </summary>
    /// <param name="instance">instance</param>
    public BCPPathManager(Instance instance)
        : base(instance)
    {
        //Need a Request on Fail
        BotNormal.RequestReoptimizationAfterFailingOfNextWaypointReservation = true;

        //translate to lightweight graph
        var graph = GenerateGraph();
        var config = instance.ControllerConfig.PathPlanningConfig as BCPPathPlanningConfiguration;

        PathFinder = new BCPMethod(graph, instance.SettingConfig.Seed, new PathPlanningCommunicator(
            instance.LogSevere,
            instance.LogDefault,
            instance.LogInfo,
            instance.LogVerbose,
            () => { instance.StatOverallPathPlanningTimeouts++; }));
        var method = PathFinder as BCPMethod;
        method.LengthOfAWaitStep = config.LengthOfAWaitStep;
        method.RuntimeLimitPerAgent = config.RuntimeLimitPerAgent;
        method.RunTimeLimitOverall = config.RunTimeLimitOverall;
        method.BiasedCostAmount = config.BiasedCostAmount;

        if (config.AutoSetParameter)
        {
            //best parameter determined my master thesis
            method.BiasedCostAmount = 1;
            method.RuntimeLimitPerAgent = config.Clocking / instance.Bots.Count;
            method.RunTimeLimitOverall = config.Clocking;
        }

    }
}