using RAWSimO.Core.Configurations;
using RAWSimO.Core.Control.Defaults.ItemStorage;
using RAWSimO.Core.Control.Defaults.MethodManagement;
using RAWSimO.Core.Control.Defaults.OrderBatching;
using RAWSimO.Core.Control.Defaults.PathPlanning;
using RAWSimO.Core.Control.Defaults.PodStorage;
using RAWSimO.Core.Control.Defaults.ReplenishmentBatching;
using RAWSimO.Core.Control.Defaults.Repositioning;
using RAWSimO.Core.Control.Defaults.StationActivation;
using RAWSimO.Core.Control.Defaults.TaskAllocation;
using System;
using System.Linq;

namespace RAWSimO.Core.Control;

/// <summary>
/// The main class containing all control mechanisms for decisions conducted during simulation.
/// </summary>
public class Controller
{
    /// <summary>
    /// Creates a new controller instance.
    /// </summary>
    /// <param name="instance">The instance this controller belongs to.</param>
    public Controller(Instance instance)
    {
        Instance = instance;
        // Init path manager
        PathManager = instance.ControllerConfig.PathPlanningConfig.GetMethodType() switch
        {
            PathPlanningMethodType.Simple => null,
            PathPlanningMethodType.Dummy => new DummyPathManager(instance),
            PathPlanningMethodType.WHCAvStar => new WHCAvStarPathManager(instance),
            PathPlanningMethodType.FAR => new FARPathManager(instance),
            PathPlanningMethodType.BCP => new BCPPathManager(instance),
            PathPlanningMethodType.CBS => new CBSPathManager(instance),
            PathPlanningMethodType.OD_ID => new ODIDPathManager(instance),
            PathPlanningMethodType.WHCAnStar => new WHCAnStarPathManager(instance),
            PathPlanningMethodType.PAS => new PASPathManager(instance),
            _ => throw new ArgumentException("Unknown path planning engine: " +
                                             instance.ControllerConfig.PathPlanningConfig.GetMethodType())
        };
        // Init bot manager
        BotManager = instance.ControllerConfig.TaskAllocationConfig.GetMethodType() switch
        {
            TaskAllocationMethodType.BruteForce => new BruteForceBotManager(instance),
            TaskAllocationMethodType.Random => new RandomBotManager(instance),
            TaskAllocationMethodType.Balanced => new BalancedBotManager(instance),
            TaskAllocationMethodType.Swarm => new SwarmBotManager(instance),
            TaskAllocationMethodType.ConstantRatio => new ConstantRatioBotManager(instance),
            TaskAllocationMethodType.Concept => new ConceptBotManager(instance),
            _ => throw new ArgumentException("Unknown bot manager: " +
                                             instance.ControllerConfig.TaskAllocationConfig.GetMethodType())
        };
        // Init station manager
        StationManager = instance.ControllerConfig.StationActivationConfig.GetMethodType() switch
        {
            StationActivationMethodType.ActivateAll => new ActivateAllStationManager(instance),
            StationActivationMethodType.BacklogThreshold => new BacklogThresholdStationManager(instance),
            StationActivationMethodType.ConstantRatio => new ConstantRatioStationManager(instance),
            StationActivationMethodType.WorkShift => new WorkShiftStationActivationManager(instance),
            _ => throw new ArgumentException("Unknown station manager: " +
                                             instance.ControllerConfig.StationActivationConfig.GetMethodType())
        };
        // Init item storage manager
        StorageManager = instance.ControllerConfig.ItemStorageConfig.GetMethodType() switch
        {
            ItemStorageMethodType.Dummy => new DummyStorageManager(instance),
            ItemStorageMethodType.Random => new RandomStorageManager(instance),
            ItemStorageMethodType.Correlative => new CorrelativeStorageManager(instance),
            ItemStorageMethodType.Turnover => new TurnoverStorageManager(instance),
            ItemStorageMethodType.ClosestLocation => new ClosestLocationStorageManager(instance),
            ItemStorageMethodType.Reactive => new ReactiveStorageManager(instance),
            ItemStorageMethodType.Emptiest => new EmptiestStorageManager(instance),
            ItemStorageMethodType.LeastDemand => new LeastDemandStorageManager(instance),
            _ => throw new ArgumentException("Unknown storage manager: " +
                                             instance.ControllerConfig.ItemStorageConfig.GetMethodType())
        };
        // Init pod storage manager
        PodStorageManager = instance.ControllerConfig.PodStorageConfig.GetMethodType() switch
        {
            PodStorageMethodType.Dummy => new DummyPodStorageManager(instance),
            PodStorageMethodType.Fixed => new FixedPodStorageManager(instance),
            PodStorageMethodType.Nearest => new NearestPodStorageManager(instance),
            PodStorageMethodType.StationBased => new StationBasedPodStorageManager(instance),
            PodStorageMethodType.Cache => new CachePodStorageManager(instance),
            PodStorageMethodType.Utility => new UtilityPodStorageManager(instance),
            PodStorageMethodType.Random => new RandomPodStorageManager(instance),
            PodStorageMethodType.Turnover => new TurnoverPodStorageManager(instance),
            _ => throw new ArgumentException("Unknown pod manager: " +
                                             instance.ControllerConfig.PodStorageConfig.GetMethodType())
        };
        // Init repositioning manager
        RepositioningManager = instance.ControllerConfig.RepositioningConfig.GetMethodType() switch
        {
            RepositioningMethodType.Dummy => new DummyRepositioningManager(instance),
            RepositioningMethodType.Cache => new CacheRepositioningManager(instance),
            RepositioningMethodType.CacheDropoff => new CacheDropoffRepositioningManager(instance),
            RepositioningMethodType.Utility => new UtilityRepositioningManager(instance),
            RepositioningMethodType.Concept => new ConceptRepositioningManager(instance),
            _ => throw new ArgumentException("Unknown repositioning manager: " +
                                             instance.ControllerConfig.RepositioningConfig.GetMethodType())
        };
        // Init order batching manager
        OrderManager = instance.ControllerConfig.OrderBatchingConfig.GetMethodType() switch
        {
            OrderBatchingMethodType.Default => new DefaultOrderManager(instance),
            OrderBatchingMethodType.Random => new RandomOrderManager(instance),
            OrderBatchingMethodType.Workload => new WorkloadOrderManager(instance),
            OrderBatchingMethodType.Related => new RelatedOrderManager(instance),
            OrderBatchingMethodType.NearBestPod => new NearBestPodOrderManager(instance),
            OrderBatchingMethodType.Foresight => new ForesightOrderManager(instance),
            OrderBatchingMethodType.PodMatching => new PodMatchingOrderManager(instance),
            OrderBatchingMethodType.LinesInCommon => new LinesInCommonOrderManager(instance),
            OrderBatchingMethodType.Queue => new QueueOrderManager(instance),
            _ => throw new ArgumentException("Unknown order manager: " +
                                             instance.ControllerConfig.OrderBatchingConfig.GetMethodType())
        };
        // Init replenishment batching manger
        BundleManager = instance.ControllerConfig.ReplenishmentBatchingConfig.GetMethodType() switch
        {
            ReplenishmentBatchingMethodType.Random => new RandomBundleManager(instance),
            ReplenishmentBatchingMethodType.SamePod => new SamePodBundleManager(instance),
            _ => throw new ArgumentException("Unknown replenishment manager: " +
                                             instance.ControllerConfig.ReplenishmentBatchingConfig.GetMethodType())
        };
        // Init meta method manager
        MethodManager = instance.ControllerConfig.MethodManagementConfig.GetMethodType() switch
        {
            MethodManagementType.NoChange => new NoChangeMethodManager(instance),
            MethodManagementType.Random => new RandomMethodManager(instance),
            MethodManagementType.Scheduled => new ScheduleMethodManager(instance),
            _ => throw new ArgumentException("Unknown method manager: " +
                                             instance.ControllerConfig.MethodManagementConfig.GetMethodType())
        };
        // Init allocator
        Allocator = new Allocator(instance);
    }

    /// <summary>
    /// The instance to simulate.
    /// </summary>
    private Instance Instance { get; }
    /// <summary>
    /// The method manager.
    /// </summary>
    public MethodManager MethodManager { get; }
    /// <summary>
    /// The order manager.
    /// </summary>
    public OrderManager OrderManager { get; }
    /// <summary>
    /// The bundle manager.
    /// </summary>
    public BundleManager BundleManager { get; }
    /// <summary>
    /// The storage manager.
    /// </summary>
    public ItemStorageManager StorageManager { get; }
    /// <summary>
    /// The pod storage manager.
    /// </summary>
    public PodStorageManager PodStorageManager { get; private set; }
    /// <summary>
    /// The repositioning manager.
    /// </summary>
    public RepositioningManager RepositioningManager { get; }
    /// <summary>
    /// The station manager.
    /// </summary>
    public StationManager StationManager { get; }
    /// <summary>
    /// The bot manager.
    /// </summary>
    public BotManager BotManager { get; }
    /// <summary>
    /// The path planner.
    /// </summary>
    public PathManager PathManager { get; private set; }
    /// <summary>
    /// The allocator.
    /// </summary>
    public Allocator Allocator { get; private set; }

    /// <summary>
    /// The current time.
    /// </summary>
    private double _currentTime;

    /// <summary>
    /// The time the simulation step is completed.
    /// </summary>
    private double _updateFinishTime;

    /// <summary>
    /// The current time.
    /// </summary>
    public double CurrentTime => _currentTime;

    /// <summary>
    /// The progress of the simulation.
    /// </summary>
    public double Progress => _currentTime / (Instance.SettingConfig.SimulationWarmupTime + Instance.SettingConfig.SimulationDuration);

    /// <summary>
    /// Used to wait for workers that are still busy. (In case we simulated faster than real-time)
    /// </summary>
    /// <param name="currentTime">The current simulation time.</param>
    protected void WaitForUnfinishedWorker(double currentTime)
    {
        BotManager.SignalCurrentTime(currentTime);
        StationManager.SignalCurrentTime(currentTime);
        StorageManager.SignalCurrentTime(currentTime);
        PodStorageManager.SignalCurrentTime(currentTime);
        RepositioningManager.SignalCurrentTime(currentTime);
        OrderManager.SignalCurrentTime(currentTime);
        BundleManager.SignalCurrentTime(currentTime);
    }

    /// <summary>
    /// Moves the simulation forward by the specified amount of time.
    /// </summary>
    /// <param name="elapsedTime">The relative amount of time by which the simulation is forwarded.</param>
    public void Update(double elapsedTime)
    {
        // Don't want to update less than the time required for something to move past 1/3 of the tolerance in a given time interval
        // TODO this probably results in inaccurate timing statistics - is it necessary to change this? (minimum updatetime influences constant times of tasks - they are not constant anymore, because their finish event might be skipped)
        var minimumUpdateTime = Instance.SettingConfig.Tolerance / 3.0 / Instance.Bots.Max(b => b.MaxVelocity);

        _updateFinishTime = _currentTime + elapsedTime;
        while (_currentTime < _updateFinishTime)
        {
            // --> Get the next event time
            var nextTime =
                Math.Min(_updateFinishTime, // Stop after all time is elapsed
                    Math.Min(MethodManager.GetNextEventTime(_currentTime), // Check the meta manager
                        Instance.Updateables.Min(u => u.GetNextEventTime(_currentTime)))); // Jump to next event of all agents

            // See if a potential collision will happen before the next event
            var minTimeDelta = Math.Min(Instance.Compound.GetShortestTimeWithoutCollision(), nextTime - _currentTime);
            minTimeDelta = Math.Max(minTimeDelta, minimumUpdateTime);	// Make sure update rate never gets too slow

            // Update by at least the minimum, but don't go past the next time
            nextTime = Math.Min(_updateFinishTime, _currentTime + minTimeDelta);

            // Wait for unfinished optimization workers
            WaitForUnfinishedWorker(nextTime);

            // --> Run up til the next event
            // Update method manager (needs to be updated first, because it might change the update-list)
            MethodManager.Update(_currentTime, nextTime);
            // Update all agents in the list
            foreach (var updateable in Instance.Updateables)
                updateable.Update(_currentTime, nextTime);

            // Set new time
            _currentTime = nextTime;
        }
    }

    #region Manager exchange handling

    /// <summary>
    /// Exchanges the active pod storage manager with the given one.
    /// </summary>
    /// <param name="newManager">The new manager.</param>
    public void ExchangePodStorageManager(PodStorageManager newManager)
    {
        Instance.RemoveUpdateable(PodStorageManager);
        PodStorageManager = newManager;
        Instance.AddUpdateable(newManager);
    }

    #endregion

}