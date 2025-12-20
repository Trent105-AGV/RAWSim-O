using RAWSimO.Core.Configurations;
using RAWSimO.Core.Control.Shared;
using RAWSimO.Core.Elements;
using RAWSimO.Core.IO;
using RAWSimO.Core.Metrics;
using RAWSimO.Core.Waypoints;
using System;
using System.Linq;

namespace RAWSimO.Core.Control.Defaults.PodStorage;

/// <summary>
/// Supplies a turnover based pod storage manager.
/// </summary>
public class TurnoverPodStorageManager : PodStorageManager
{
    /// <summary>
    /// Creates a new instance of this manager.
    /// </summary>
    /// <param name="instance">The instance this manager belongs to.</param>
    public TurnoverPodStorageManager(Instance instance) : base(instance)
    {
        _config = instance.ControllerConfig.PodStorageConfig as TurnoverPodStorageConfiguration;
        // Initialize class manager
        _classManager = instance.SharedControlElements.TurnoverClassBuilder;
        _classManager.ParseConfigAndEnsureCompatibility(
            _config.ClassBorders.Split(IOConstants.DELIMITER_LIST).Select(e => double.Parse(e, IOConstants.FORMATTER)).OrderBy(v => v).ToArray(),
            _config.ReallocationDelay,
            _config.ReallocationOrderCount);
    }

    /// <summary>
    /// The config for this manager.
    /// </summary>
    private readonly TurnoverPodStorageConfiguration _config;
    /// <summary>
    /// The class manager in use.
    /// </summary>
    private readonly TurnoverClassBuilder _classManager;

    /// <summary>
    /// Chooses the storage location to use for the given pod.
    /// </summary>
    /// <param name="pod">The pod to store.</param>
    /// <returns>The storage location to use for the pod.</returns>
    private Waypoint ChooseStorageLocation(Pod pod)
    {
        // Get the storage class the pod should end up in
        var desiredStorageClass = _classManager.DetermineStorageClass(pod);
        // Try to allocate the pod to its storage class - if not possible try neighboring classes
        var currentClassTriedLow = desiredStorageClass; var currentClassTriedHigh = desiredStorageClass;
        Waypoint chosenStorageLocation = null;
        while (true)
        {
            // Try the less frequent class first
            if (currentClassTriedLow < _classManager.ClassCount)
                chosenStorageLocation = _classManager.GetClassStorageLocations(currentClassTriedLow)
                    .Where(wp => !Instance.ResourceManager.IsStorageLocationClaimed(wp)) // Only use not occupied ones
                    .OrderBy(wp =>
                    {
                        return _config.PodDisposeRule switch
                        {
                            TurnoverPodStorageLocationDisposeRule.NearestEuclid => Distances.CalculateEuclid(wp, pod,
                                Instance.WrongTierPenaltyDistance),
                            TurnoverPodStorageLocationDisposeRule.NearestManhattan => Distances.CalculateManhattan(wp,
                                pod, Instance.WrongTierPenaltyDistance),
                            TurnoverPodStorageLocationDisposeRule.NearestShortestPath =>
                                Distances.CalculateShortestPathPodSafe(
                                    Instance.WaypointGraph.GetClosestWaypoint(pod.Tier, pod.X, pod.Y), wp, Instance),
                            TurnoverPodStorageLocationDisposeRule.NearestShortestTime =>
                                Distances.CalculateShortestTimePathPodSafe(
                                    Instance.WaypointGraph.GetClosestWaypoint(pod.Tier, pod.X, pod.Y), wp, Instance),
                            TurnoverPodStorageLocationDisposeRule.OStationNearestEuclid =>
                                Instance.OutputStations.Min(s =>
                                    Distances.CalculateEuclid(wp, s, Instance.WrongTierPenaltyDistance)),
                            TurnoverPodStorageLocationDisposeRule.OStationNearestManhattan =>
                                Instance.OutputStations.Min(s =>
                                    Distances.CalculateManhattan(wp, s, Instance.WrongTierPenaltyDistance)),
                            TurnoverPodStorageLocationDisposeRule.OStationNearestShortestPath =>
                                Instance.OutputStations.Min(s =>
                                    Distances.CalculateShortestPathPodSafe(wp, s.Waypoint, Instance)),
                            TurnoverPodStorageLocationDisposeRule.OStationNearestShortestTime =>
                                Instance.OutputStations.Min(s =>
                                    Distances.CalculateShortestTimePathPodSafe(wp, s.Waypoint, Instance)),
                            TurnoverPodStorageLocationDisposeRule.Random => wp.Instance.Randomizer.NextDouble(),
                            _ => throw new ArgumentException("Unknown pod dispose rule: " + _config.PodDisposeRule)
                        };
                    }) // Order the remaining ones by the given rule
                    .FirstOrDefault(); // Use the first one
            // Check whether we found a suitable pod of this class
            if (chosenStorageLocation != null)
                break;
            // Try the higher frequent class next
            if (currentClassTriedHigh >= 0 && currentClassTriedHigh != currentClassTriedLow)
                chosenStorageLocation = _classManager.GetClassStorageLocations(currentClassTriedHigh)
                    .Where(wp => !Instance.ResourceManager.IsStorageLocationClaimed(wp)) // Only use not occupied ones
                    .OrderBy(wp =>
                    {
                        return _config.PodDisposeRule switch
                        {
                            TurnoverPodStorageLocationDisposeRule.NearestEuclid => Distances.CalculateEuclid(wp, pod,
                                Instance.WrongTierPenaltyDistance),
                            TurnoverPodStorageLocationDisposeRule.NearestManhattan => Distances.CalculateManhattan(wp,
                                pod, Instance.WrongTierPenaltyDistance),
                            TurnoverPodStorageLocationDisposeRule.NearestShortestPath =>
                                Distances.CalculateShortestPathPodSafe(
                                    Instance.WaypointGraph.GetClosestWaypoint(pod.Tier, pod.X, pod.Y), wp, Instance),
                            TurnoverPodStorageLocationDisposeRule.NearestShortestTime =>
                                Distances.CalculateShortestTimePathPodSafe(
                                    Instance.WaypointGraph.GetClosestWaypoint(pod.Tier, pod.X, pod.Y), wp, Instance),
                            TurnoverPodStorageLocationDisposeRule.OStationNearestEuclid =>
                                Instance.OutputStations.Min(s =>
                                    Distances.CalculateEuclid(wp, s, Instance.WrongTierPenaltyDistance)),
                            TurnoverPodStorageLocationDisposeRule.OStationNearestManhattan =>
                                Instance.OutputStations.Min(s =>
                                    Distances.CalculateManhattan(wp, s, Instance.WrongTierPenaltyDistance)),
                            TurnoverPodStorageLocationDisposeRule.OStationNearestShortestPath =>
                                Instance.OutputStations.Min(s =>
                                    Distances.CalculateShortestPathPodSafe(wp, s.Waypoint, Instance)),
                            TurnoverPodStorageLocationDisposeRule.OStationNearestShortestTime =>
                                Instance.OutputStations.Min(s =>
                                    Distances.CalculateShortestTimePathPodSafe(wp, s.Waypoint, Instance)),
                            TurnoverPodStorageLocationDisposeRule.Random => wp.Instance.Randomizer.NextDouble(),
                            _ => throw new ArgumentException("Unknown pod dispose rule: " + _config.PodDisposeRule)
                        };
                    }) // Order the remaining ones by the given rule
                    .FirstOrDefault(); // Use the first one
            // Check whether we found a suitable pod of this class
            if (chosenStorageLocation != null)
                break;
            // Update the class indeces to check next
            currentClassTriedLow++; currentClassTriedHigh--;
            // Check index correctness
            if (currentClassTriedHigh < 0 && currentClassTriedLow >= _classManager.ClassCount)
                throw new InvalidOperationException("There was no storage location available!");
        }
        // Return the chosen one
        return chosenStorageLocation;
    }

    /// <summary>
    /// Determines the storage location for the given pod.
    /// </summary>
    /// <param name="pod">The pod to store.</param>
    /// <returns>The storage location to use for storing the pod.</returns>
    protected override Waypoint GetStorageLocationForPod(Pod pod)
    {
        // Choose
        var chosenLocation = ChooseStorageLocation(pod);
        // Check success
        if (chosenLocation == null)
            throw new InvalidOperationException("There was no suitable storage location for the pod: " + pod);
        // Return it
        return chosenLocation;
    }

    #region IOptimize Members

    /// <summary>
    /// Signals the current time to the mechanism. The mechanism can decide to block the simulation thread in order consume remaining real-time.
    /// </summary>
    /// <param name="currentTime">The current simulation time.</param>
    public override void SignalCurrentTime(double currentTime) { /* Ignore since this simple manager is always ready. */ }

    #endregion
}