using RAWSimO.Core.Configurations;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Items;
using RAWSimO.Core.Metrics;
using RAWSimO.Toolbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RAWSimO.Core.Control.Defaults.ReplenishmentBatching;

/// <summary>
/// Indicates a rule that is used for selecting the first station for a new bundle.
/// </summary>
public enum SamePodFirstStationRule
{
    /// <summary>
    /// Uses the emptiest station.
    /// </summary>
    Emptiest,
    /// <summary>
    /// Uses the fullest station.
    /// </summary>
    Fullest,
    /// <summary>
    /// Uses the one with the fewest number of allocated bundles.
    /// </summary>
    LeastBusy,
    /// <summary>
    /// Uses the one with the highest number of allocated bundles.
    /// </summary>
    MostBusy,
    /// <summary>
    /// Uses a random station.
    /// </summary>
    Random,
    /// <summary>
    /// Uses the station with the shortest euclidean distance to the station.
    /// </summary>
    DistanceEuclid,
}
/// <summary>
/// Implements a manager that aims to use the same station for all bundles that shall go into the same pod.
/// </summary>
public class SamePodBundleManager : BundleManager
{
    /// <summary>
    /// Creates a new instance of this manager.
    /// </summary>
    /// <param name="instance">The instance this manager belongs to.</param>
    public SamePodBundleManager(Instance instance) : base(instance)
    {
        _config = instance.ControllerConfig.ReplenishmentBatchingConfig as SamePodReplenishmentBatchingConfiguration;
        Instance.ItemStorageAllocationAvailable += SignalItemStorageAllocationAvailable;
    }

    /// <summary>
    /// The config of this controller.
    /// </summary>
    private readonly SamePodReplenishmentBatchingConfiguration _config;
    /// <summary>
    /// The item bundles to assign.
    /// </summary>
    private readonly List<ItemBundle> _itemBundles = [];
    ///// <summary>
    ///// All bundles that are assigned to a pod.
    ///// </summary>
    private readonly Dictionary<ItemBundle, Pod> _bundleToPod = new();
    private readonly Dictionary<Pod, List<ItemBundle>> _podBundles = new();
    private readonly Dictionary<Pod, double> _waitingTime = new();
    /// <summary>
    /// The station chosen last time for the pod.
    /// </summary>
    private readonly Dictionary<Pod, InputStation> _lastChosenStations = new();

    /// <summary>
    /// This is called to decide about potentially pending bundles.
    /// This method is being timed for statistical purposes and is also ONLY called when <code>SituationInvestigated</code> is <code>false</code>.
    /// Hence, set the field accordingly to react on events not tracked by this outer skeleton.
    /// </summary>
    protected override void DecideAboutPendingBundles()
    {
        if (_config.BreakBatches)
        {
            // Go through list of not assigned bundles
            for (var i = 0; i < _itemBundles.Count; i++)
            {
                // Check which pod was used to store the bundle
                var bundle = _itemBundles[i];
                var podForBundle = _bundleToPod[bundle];
                // See whether we already have a station in memory for this pod
                InputStation chosenStation;
                if (_lastChosenStations.ContainsKey(podForBundle))
                {
                    // See whether we can assign the new bundle to that station
                    if (_lastChosenStations[podForBundle].FitsForReservation(bundle))
                    {
                        chosenStation = _lastChosenStations[podForBundle];
                    }
                    else
                    {
                        // The bundle won't fit the station - try a station close by
                        chosenStation = Instance.InputStations
                            .Where(s => s.Active && s.FitsForReservation(bundle)) // There has to be sufficient capacity left at the station and the station needs to be active
                            .OrderBy(s => Distances.CalculateEuclid(_lastChosenStations[podForBundle], s, Instance.WrongTierPenaltyDistance)) // Start with the nearest station
                            .FirstOrDefault(); // Return the first one or null if none available
                    }
                }
                else
                {
                    // We don't know this pod - select a new station
                    chosenStation = _config.FirstStationRule switch
                    {
                        SamePodFirstStationRule.Emptiest => Instance.InputStations
                            .Where(s => s.Active &&
                                        s.FitsForReservation(
                                            bundle)) // There has to be sufficient capacity left at the station and the station needs to be active
                            .OrderBy(s => (s.CapacityInUse + s.CapacityReserved) / s.Capacity) // Pick the emptiest one
                            .FirstOrDefault() // Return the first one or null if none available
                        ,
                        SamePodFirstStationRule.Fullest => Instance.InputStations
                            .Where(s => s.Active &&
                                        s.FitsForReservation(
                                            bundle)) // There has to be sufficient capacity left at the station and the station needs to be active
                            .OrderByDescending(s =>
                                (s.CapacityInUse + s.CapacityReserved) / s.Capacity) // Pick the fullest one
                            .FirstOrDefault() // Return the first one or null if none available
                        ,
                        SamePodFirstStationRule.LeastBusy => Instance.InputStations
                            .Where(s => s.Active &&
                                        s.FitsForReservation(
                                            bundle)) // There has to be sufficient capacity left at the station and the station needs to be active
                            .OrderBy(s => s.ItemBundles.Count()) // Pick the one with the fewest bundles
                            .FirstOrDefault() // Return the first one or null if none available
                        ,
                        SamePodFirstStationRule.MostBusy => Instance.InputStations
                            .Where(s => s.Active &&
                                        s.FitsForReservation(
                                            bundle)) // There has to be sufficient capacity left at the station and the station needs to be active
                            .OrderByDescending(s => s.ItemBundles.Count()) // Pick the one with the most bundles
                            .FirstOrDefault() // Return the first one or null if none available
                        ,
                        SamePodFirstStationRule.Random => Instance.InputStations
                            .Where(s => s.Active &&
                                        s.FitsForReservation(
                                            bundle)) // There has to be sufficient capacity left at the station and the station needs to be active
                            .OrderBy(s => Instance.Randomizer.NextDouble()) // Pick a random one
                            .FirstOrDefault() // Return the first one or null if none available
                        ,
                        SamePodFirstStationRule.DistanceEuclid => Instance.InputStations
                            .Where(s => s.Active &&
                                        s.FitsForReservation(
                                            bundle)) // There has to be sufficient capacity left at the station and the station needs to be active
                            .OrderBy(s =>
                                Distances.CalculateEuclid(podForBundle, s,
                                    Instance.WrongTierPenaltyDistance)) // Pick the nearest one
                            .FirstOrDefault() // Return the first one or null if none available
                        ,
                        _ => throw new ArgumentException("Unknown first station rule: " + _config.FirstStationRule)
                    };
                }
                // If we found a station, assign the bundle to it
                if (chosenStation != null)
                {
                    AddToReadyList(bundle, chosenStation);
                    _bundleToPod.Remove(bundle);
                    _itemBundles.RemoveAt(0);
                    i--;
                    _lastChosenStations[podForBundle] = chosenStation;
                }
            }
        }
        else
        {
            // Assign batches in the order they arrive in
            List<Pod> removedBatches = null;
            foreach (var batch in _podBundles.OrderBy(p => _waitingTime[p.Key]))
            {
                var batchSize = batch.Value.Sum(b => b.BundleWeight);
                // Choose a suitable station
                var chosenStation = Instance.InputStations
                    // Only active stations where the complete bundle fits
                    .Where(s => s.Active && batchSize <= s.RemainingCapacity)
                    // Only stations that are located on the same tier as the pod (if desired)
                    .Where(s => s.Tier == batch.Key.Tier)
                    // Find the best station according to the chosen rule
                    .ArgMin(s =>
                    {
                        return _config.FirstStationRule switch
                        {
                            SamePodFirstStationRule.Emptiest => (s.CapacityInUse + s.CapacityReserved) / s.Capacity,
                            SamePodFirstStationRule.Fullest =>
                                1 - (s.CapacityInUse + s.CapacityReserved) / s.Capacity,
                            SamePodFirstStationRule.LeastBusy => s.ItemBundles.Count(),
                            SamePodFirstStationRule.MostBusy => -s.ItemBundles.Count(),
                            SamePodFirstStationRule.Random => Instance.Randomizer.NextDouble(),
                            SamePodFirstStationRule.DistanceEuclid => Distances.CalculateEuclid(batch.Key, s,
                                Instance.WrongTierPenaltyDistance),
                            _ => throw new ArgumentException("Unknown rule: " + _config.FirstStationRule)
                        };
                    });
                // Check whether there was a suitable station at all
                if (chosenStation != null)
                {
                    // Submit the decision
                    foreach (var bundle in batch.Value)
                        AddToReadyList(bundle, chosenStation);
                    // Clean up
                    _lastChosenStations[batch.Key] = chosenStation;
                    if (removedBatches == null)
                        removedBatches = [];
                    removedBatches.Add(batch.Key);
                }
                else
                {
                    // If FCFS applies, we cannot assign further batches until this one gets assigned
                    if (_config.FCFS)
                        break;
                }
            }
            // Actually remove the batches
            if (removedBatches != null)
                foreach (var batch in removedBatches)
                    _podBundles.Remove(batch);
        }
    }

    private void SignalItemStorageAllocationAvailable(Pod pod, ItemBundle bundle)
    {
        // This is another event this controller should react upon
        SituationInvestigated = false;
        // Add the bundle to the todo list
        _itemBundles.Add(bundle);
        // Save the pod where the bundle is assigned to
        _bundleToPod[bundle] = pod;
        // Store the time the batch was first seen
        if (!_podBundles.ContainsKey(pod) || !_podBundles[pod].Any())
            _waitingTime[pod] = Instance.Controller.CurrentTime;
        // Assign the bundle to the batch or create a new one
        if (_podBundles.ContainsKey(pod))
            _podBundles[pod].Add(bundle);
        else
            _podBundles[pod] = [bundle];
    }

    #region IOptimize Members

    /// <summary>
    /// Signals the current time to the mechanism. The mechanism can decide to block the simulation thread in order consume remaining real-time.
    /// </summary>
    /// <param name="currentTime">The current simulation time.</param>
    public override void SignalCurrentTime(double currentTime) { /* Ignore since this simple manager is always ready. */ }

    #endregion
}