using RAWSimO.Core.Bots;
using RAWSimO.MultiAgentPathFinding;
using RAWSimO.MultiAgentPathFinding.Methods;

namespace RAWSimO.Core.Control.Defaults.PathPlanning
{

    /// <summary>
    /// Controller of the bot.
    /// </summary>
    public class DummyPathManager : PathManager
    {

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="instance">instance</param>
        public DummyPathManager(Instance instance)
            : base(instance)
        {
            //Need a Request on Fail
            BotNormal.RequestReoptimizationAfterFailingOfNextWaypointReservation = true;

            //translate to lightweight graph
            var graph = GenerateGraph();

            PathFinder = new DummyMethod(graph, instance.SettingConfig.Seed, new PathPlanningCommunicator(
                instance.LogSevere,
                instance.LogDefault,
                instance.LogInfo,
                instance.LogVerbose,
                () => { instance.StatOverallPathPlanningTimeouts++; }));
            var method = PathFinder as DummyMethod;
            method.LengthOfAWaitStep = instance.ControllerConfig.PathPlanningConfig.LengthOfAWaitStep;
            method.RuntimeLimitPerAgent = instance.ControllerConfig.PathPlanningConfig.RuntimeLimitPerAgent;
            method.RunTimeLimitOverall = instance.ControllerConfig.PathPlanningConfig.RunTimeLimitOverall;
        }
    }
}
