using RAWSimO.Core.Configurations;
using RAWSimO.Core.Control.Shared;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Metrics;
using RAWSimO.Core.Waypoints;
using RAWSimO.Toolbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RAWSimO.Core.Control.Defaults.PodStorage;

/// <summary>
/// Implements a pod storage manager that aims to use the next free storage location.
/// </summary>
public class CachePodStorageManager : PodStorageManager
{
    /// <summary>
    /// Creates a new instance of the manager.
    /// </summary>
    /// <param name="instance">The instance this manager belongs to.</param>
    public CachePodStorageManager(Instance instance) : base(instance) { _config = instance.ControllerConfig.PodStorageConfig as CachePodStorageConfiguration; }

    /// <summary>
    /// The config for this manager.
    /// </summary>
    private readonly CachePodStorageConfiguration _config;

    // TODO implement initial positioning rule by simply ordering all pods by their frequency and placing them accordingly - every xth index needs to be unused to avoid overstuffing the front area - also: implement almost or even the same rule for the utility manager

    /// <summary>
    /// Contains information about the station to which a certain cache storage location belongs.
    /// </summary>
    private VolatileIDDictionary<Waypoint, OutputStation> _cacheStations;

    /// <summary>
    /// A helper used to determine the best storage location.
    /// </summary>
    private BestCandidateSelector _bestCandidateSelector;
    /// <summary>
    /// A helper variable storing the current cache info of the pod.
    /// </summary>
    private bool _currentCacheable;
    /// <summary>
    /// The current station the pod is leaving.
    /// </summary>
    private OutputStation _currentStation;
    /// <summary>
    /// A helper variable storing the current pod location to assess.
    /// </summary>
    private Waypoint _currentPodLocation;
    /// <summary>
    /// A helper variable storing the current waypoint to assess.
    /// </summary>
    private Waypoint _currentStorageLocation;

    /// <summary>
    /// Initializes this manager.
    /// </summary>
    private void Init()
    {
        // --> Init shared component or ensure consistency
        Instance.SharedControlElements.StoragePartitioner.CreateOrEnsureZones(_config.ZoningConfiguration);
        // --> Store information about the station to which a cache storage location belongs
        _cacheStations = new VolatileIDDictionary<Waypoint, OutputStation>(
            Instance.SharedControlElements.StoragePartitioner.CachePartitions.SelectMany(kvp => kvp.Value.Select(wp => new VolatileKeyValuePair<Waypoint, OutputStation>(wp, kvp.Key))).ToList());
        // --> Init scoring
        var scorers = new List<Func<double>>
        {
            // First select cache or not depending on decision
            () =>
            {
                switch (_currentCacheable)
                {
                    // Check whether the current location belongs to the current station's cache and the pod does make the cut according to the cache score
                    // It fits, reward it
                    case true when _currentStorageLocation.InfoTagCache == ZoneType.Cache && _cacheStations[_currentStorageLocation] == _currentStation:
                    // Check whether the pod shouldn't be cached and the storage location does not belong to a cache
                    // It fits, reward it
                    case false when _currentStorageLocation.InfoTagCache != ZoneType.Cache:
                        return -1;
                }

                // Check whether the storage location belongs to a foreign station's cache
                return _currentStorageLocation.InfoTagCache == ZoneType.Cache ?
                    // Heavily penalize using foreign cache storage locations
                    1 :
                    // It does not fit, penalize it
                    0;
            },
            // Then select the nearest one
            () =>
            {
                return _config.PodDisposeRule switch
                {
                    CacheStorageLocationSelectionRule.Euclid => Distances.CalculateEuclid(_currentPodLocation,
                        _currentStorageLocation, Instance.WrongTierPenaltyDistance),
                    CacheStorageLocationSelectionRule.Manhattan => Distances.CalculateManhattan(_currentPodLocation,
                        _currentStorageLocation, Instance.WrongTierPenaltyDistance),
                    CacheStorageLocationSelectionRule.ShortestPath => Distances.CalculateShortestPathPodSafe(
                        _currentPodLocation, _currentStorageLocation, Instance),
                    CacheStorageLocationSelectionRule.ShortestTime => Distances.CalculateShortestTimePathPodSafe(
                        _currentPodLocation, _currentStorageLocation, Instance),
                    _ => throw new ArgumentException("Unknown pod dispose rule: " + _config.PodDisposeRule)
                };
            }
        };
        // Instantiate best candidate assessment
        _bestCandidateSelector = new BestCandidateSelector(false, scorers.ToArray());
    }

    /// <summary>
    /// Returns a suitable storage location for the given pod.
    /// </summary>
    /// <param name="pod">The pod to fetch a storage location for.</param>
    /// <returns>The storage location to use.</returns>
    protected override Waypoint GetStorageLocationForPod(Pod pod)
    {
        // Ensure init
        if (_bestCandidateSelector == null)
            Init();
        // Get output station this pod is coming from (it has to be the nearest one)
        _currentStation = Instance.OutputStations.ArgMin(s => Distances.CalculateEuclid(s, pod, Instance.WrongTierPenaltyDistance));
        // Get current pod location (pod has to be at station's position right now)
        _currentPodLocation = _currentStation.Waypoint;
        // Determine whether the pod should be put in the cache
        var cacheUtilityValue =
            Instance.SharedControlElements.StoragePartitioner.GetCacheValue(pod, _currentStation, _config.WeightSpeed, _config.WeightUtility);
        var cacheFill =
            (double)Instance.SharedControlElements.StoragePartitioner.CachePartitions[_currentStation].Count(c => c.Pod != null) /
            Instance.SharedControlElements.StoragePartitioner.CachePartitions[_currentStation].Count;
        _currentCacheable = ((1 - cacheFill) * _config.WeightCacheFill + cacheUtilityValue * _config.WeightCacheUtility) / 2.0 > _config.PodCacheableThreshold;
        // Get the best storage location
        _bestCandidateSelector.Recycle();
        Waypoint bestStorageLocation = null;
        foreach (var storageLocation in Instance.ResourceManager.UnusedPodStorageLocations)
        {
            // Update current candidate
            _currentStorageLocation = storageLocation;
            // Assess new candidate
            if (_bestCandidateSelector.Reassess())
                bestStorageLocation = _currentStorageLocation;
        }
        // Return it
        return bestStorageLocation;
    }

    #region IOptimize Members

    /// <summary>
    /// Signals the current time to the mechanism. The mechanism can decide to block the simulation thread in order consume remaining real-time.
    /// </summary>
    /// <param name="currentTime">The current simulation time.</param>
    public override void SignalCurrentTime(double currentTime) { /* Ignore since this simple manager is always ready. */ }

    #endregion
}