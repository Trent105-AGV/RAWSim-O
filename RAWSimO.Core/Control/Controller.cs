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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAWSimO.Core.Control;

/// <summary>
/// The main class containing all control mechanisms for decisions conducted during simulation.
/// </summary>
public class Controller
{
    private const int ParallelUpdateablesMinThreshold = 128;

    private readonly bool _profilingEnabled;
    private readonly long _profilingLogIntervalTicks;
    private readonly int _profilingTopUpdateables;

    private readonly Dictionary<string, long> _profileUpdateableTicks = new(StringComparer.Ordinal);

    private long _profilingNextLogTimestamp;
    private long _profileTotalTicks;
    private long _profileGetNextEventTicks;
    private long _profileCollisionWindowTicks;
    private long _profileWaitWorkerTicks;
    private long _profileMethodManagerUpdateTicks;
    private long _profileUpdateablesUpdateTicks;
    private long _profileStepCount;

    /// <summary>
    /// Creates a new controller instance.
    /// </summary>
    /// <param name="instance">The instance this controller belongs to.</param>
    public Controller(Instance instance)
    {
        Instance = instance;

        _profilingEnabled = instance.SettingConfig.EnablePerformanceProfiling;
        _profilingLogIntervalTicks = TimeSpan.FromMilliseconds(
            Math.Max(500, instance.SettingConfig.PerformanceProfilingLogIntervalMs)).Ticks;
        _profilingTopUpdateables = Math.Max(1, instance.SettingConfig.PerformanceProfilingTopUpdateables);
        if (_profilingEnabled)
            _profilingNextLogTimestamp = DateTime.UtcNow.Ticks + _profilingLogIntervalTicks;

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
            var loopStartTs = _profilingEnabled ? Stopwatch.GetTimestamp() : 0;
            var updateables = Instance.Updateables as IList<Interfaces.IUpdateable> ?? Instance.Updateables.ToList();

            // --> Get the next event time
            var getNextEventStartTs = _profilingEnabled ? Stopwatch.GetTimestamp() : 0;
            var nextTime =
                Math.Min(_updateFinishTime, // Stop after all time is elapsed
                    Math.Min(MethodManager.GetNextEventTime(_currentTime), // Check the meta manager
                        GetNextUpdateableEventTime(updateables, _currentTime))); // Jump to next event of all agents
            if (_profilingEnabled)
                _profileGetNextEventTicks += ToTimeSpanTicks(getNextEventStartTs, Stopwatch.GetTimestamp());

            // See if a potential collision will happen before the next event
            var collisionStartTs = _profilingEnabled ? Stopwatch.GetTimestamp() : 0;
            var minTimeDelta = Math.Min(Instance.Compound.GetShortestTimeWithoutCollision(), nextTime - _currentTime);
            minTimeDelta = Math.Max(minTimeDelta, minimumUpdateTime);	// Make sure update rate never gets too slow
            if (_profilingEnabled)
                _profileCollisionWindowTicks += ToTimeSpanTicks(collisionStartTs, Stopwatch.GetTimestamp());

            // Update by at least the minimum, but don't go past the next time
            nextTime = Math.Min(_updateFinishTime, _currentTime + minTimeDelta);

            // Wait for unfinished optimization workers
            var waitStartTs = _profilingEnabled ? Stopwatch.GetTimestamp() : 0;
            WaitForUnfinishedWorker(nextTime);
            if (_profilingEnabled)
                _profileWaitWorkerTicks += ToTimeSpanTicks(waitStartTs, Stopwatch.GetTimestamp());

            // --> Run up til the next event
            // Update method manager (needs to be updated first, because it might change the update-list)
            var methodUpdateStartTs = _profilingEnabled ? Stopwatch.GetTimestamp() : 0;
            MethodManager.Update(_currentTime, nextTime);
            if (_profilingEnabled)
                _profileMethodManagerUpdateTicks += ToTimeSpanTicks(methodUpdateStartTs, Stopwatch.GetTimestamp());

            // Update all agents in the list
            var updateablesUpdateStartTs = _profilingEnabled ? Stopwatch.GetTimestamp() : 0;
            foreach (var updateable in Instance.Updateables)
            {
                if (_profilingEnabled)
                {
                    var updateableStartTs = Stopwatch.GetTimestamp();
                    updateable.Update(_currentTime, nextTime);
                    var elapsedTicks = ToTimeSpanTicks(updateableStartTs, Stopwatch.GetTimestamp());
                    var updateableName = updateable.GetType().Name;
                    if (_profileUpdateableTicks.TryGetValue(updateableName, out var currentTicks))
                        _profileUpdateableTicks[updateableName] = currentTicks + elapsedTicks;
                    else
                        _profileUpdateableTicks[updateableName] = elapsedTicks;
                }
                else
                {
                    updateable.Update(_currentTime, nextTime);
                }
            }

            if (_profilingEnabled)
                _profileUpdateablesUpdateTicks += ToTimeSpanTicks(updateablesUpdateStartTs, Stopwatch.GetTimestamp());

            // Set new time
            _currentTime = nextTime;

            if (_profilingEnabled)
            {
                _profileTotalTicks += ToTimeSpanTicks(loopStartTs, Stopwatch.GetTimestamp());
                _profileStepCount++;
                EmitProfilingLogIfDue();
            }
        }
    }

    private static long ToTimeSpanTicks(long startTimestamp, long endTimestamp)
    {
        return (endTimestamp - startTimestamp) * TimeSpan.TicksPerSecond / Stopwatch.Frequency;
    }

    private void EmitProfilingLogIfDue()
    {
        var nowTicks = DateTime.UtcNow.Ticks;
        if (nowTicks < _profilingNextLogTimestamp)
            return;

        _profilingNextLogTimestamp = nowTicks + _profilingLogIntervalTicks;
        if (_profileTotalTicks <= 0)
            return;

        var totalMs = TimeSpan.FromTicks(_profileTotalTicks).TotalMilliseconds;
        var getNextMs = TimeSpan.FromTicks(_profileGetNextEventTicks).TotalMilliseconds;
        var collisionMs = TimeSpan.FromTicks(_profileCollisionWindowTicks).TotalMilliseconds;
        var waitMs = TimeSpan.FromTicks(_profileWaitWorkerTicks).TotalMilliseconds;
        var methodMs = TimeSpan.FromTicks(_profileMethodManagerUpdateTicks).TotalMilliseconds;
        var updateablesMs = TimeSpan.FromTicks(_profileUpdateablesUpdateTicks).TotalMilliseconds;

        var avgStepMs = _profileStepCount > 0 ? totalMs / _profileStepCount : 0.0;
        var stage = _currentTime < Instance.SettingConfig.SimulationWarmupTime ? "warmup" : "runtime";

        var builder = new StringBuilder();
        builder.Append(
            $"[Perf][{stage}] sim_t={_currentTime:0.###}s steps={_profileStepCount} avg_step={avgStepMs:0.###}ms ");
        builder.Append(
            $"total={totalMs:0.###}ms next={getNextMs:0.###}ms ({Percent(getNextMs, totalMs):0.0}%) ");
        builder.Append(
            $"collision={collisionMs:0.###}ms ({Percent(collisionMs, totalMs):0.0}%) ");
        builder.Append($"wait={waitMs:0.###}ms ({Percent(waitMs, totalMs):0.0}%) ");
        builder.Append($"method={methodMs:0.###}ms ({Percent(methodMs, totalMs):0.0}%) ");
        builder.Append($"update={updateablesMs:0.###}ms ({Percent(updateablesMs, totalMs):0.0}%)");

        var top = _profileUpdateableTicks
            .OrderByDescending(kv => kv.Value)
            .Take(_profilingTopUpdateables)
            .Select(kv =>
                $"{kv.Key}={TimeSpan.FromTicks(kv.Value).TotalMilliseconds:0.###}ms({Percent(TimeSpan.FromTicks(kv.Value).TotalMilliseconds, totalMs):0.0}%)")
            .ToArray();
        if (top.Length > 0)
            builder.Append(" | top_updateables: " + string.Join(", ", top));

        Instance.LogDefault(builder.ToString());

        _profileTotalTicks = 0;
        _profileGetNextEventTicks = 0;
        _profileCollisionWindowTicks = 0;
        _profileWaitWorkerTicks = 0;
        _profileMethodManagerUpdateTicks = 0;
        _profileUpdateablesUpdateTicks = 0;
        _profileStepCount = 0;
        _profileUpdateableTicks.Clear();
    }

    private static double Percent(double part, double total)
    {
        if (total <= 0)
            return 0;

        return (part / total) * 100.0;
    }

    private static double GetNextUpdateableEventTime(IList<Interfaces.IUpdateable> updateables, double currentTime)
    {
        if (updateables.Count == 0)
            return double.PositiveInfinity;

        if (Environment.ProcessorCount <= 1 || updateables.Count < ParallelUpdateablesMinThreshold)
        {
            var sequentialMin = double.PositiveInfinity;
            for (var i = 0; i < updateables.Count; i++)
            {
                var eventTime = updateables[i].GetNextEventTime(currentTime);
                if (eventTime < sequentialMin)
                    sequentialMin = eventTime;
            }

            return sequentialMin;
        }

        var globalMin = double.PositiveInfinity;
        var sync = new object();

        Parallel.For<double>(
            0,
            updateables.Count,
            () => double.PositiveInfinity,
            (index, _, localMin) =>
            {
                var eventTime = updateables[index].GetNextEventTime(currentTime);
                return eventTime < localMin ? eventTime : localMin;
            },
            localMin =>
            {
                lock (sync)
                {
                    if (localMin < globalMin)
                        globalMin = localMin;
                }
            });

        return globalMin;
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