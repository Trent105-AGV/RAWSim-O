using RAWSimO.MultiAgentPathFinding.DataStructures;
using RAWSimO.MultiAgentPathFinding.Elements;
using System.Collections.Generic;
using System.Linq;

namespace RAWSimO.MultiAgentPathFinding.Toolbox;

/// <summary>
/// Extract Information out of an Agent Collection
/// </summary>
public class AgentInfoExtractor
{
    /// <summary>
    /// Gets the start blockage.
    /// </summary>
    /// <param name="agents">The agents.</param>
    /// <param name="currentTime"></param>
    /// <returns></returns>
    public static Dictionary<Agent, List<ReservationTable.Interval>> GetStartBlockage(List<Agent> agents, double currentTime)
    {
        var fixedBlockage = new Dictionary<Agent, List<ReservationTable.Interval>>();

        //Reserve fixed Agents
        foreach (var fixedAgent in agents.Where(a => a.FixedPosition))
        {
            //block whole node
            fixedBlockage.Add(fixedAgent, []);
            fixedBlockage[fixedAgent].Add(new ReservationTable.Interval(fixedAgent.NextNode, currentTime, double.PositiveInfinity));
        }

        //Reserve driving Agents
        foreach (var driveAgent in agents.Where(a => !a.FixedPosition))
        {
            //block nodes needed to stop
            fixedBlockage.Add(driveAgent, driveAgent.ReservationsToNextNode.Where(r => r.End >= currentTime).ToList());
        }

        return fixedBlockage;
    }
}