using RAWSimO.Core.Configurations;
using RAWSimO.Core.Control;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Geometrics;
using RAWSimO.Core.Info;
using RAWSimO.Core.Interfaces;
using RAWSimO.Core.Metrics;
using RAWSimO.Core.Waypoints;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RAWSimO.Core.Bots;

/// <summary>
/// The legacy robot driver implementation analogue to the one used in 'Alphabet Soup'.
/// </summary>
public class BotHazard : Bot
{
    #region Constructors

    /// <summary>
    /// The configuration used for path planning.
    /// </summary>
    public SimplePathPlanningConfiguration Config { get; internal set; }
    /// <summary>
    /// Creates a new legacy bot driver object.
    /// </summary>
    /// <param name="instance">The instance this bot belongs to.</param>
    /// <param name="config">The configuration for path planning.</param>
    public BotHazard(Instance instance, SimplePathPlanningConfiguration config) : base(instance) { _botEvade = new BotEvade(this); Config = config; XTarget = double.NaN; YTarget = double.NaN; }

    #endregion

    #region DynamicAttributes

    /// <summary>
    /// The bot's targeted velocity regarding the x-dimension.
    /// </summary>
    public double XVelocityTargeted { get; private set; }

    /// <summary>
    /// The bot's targeted orientation.
    /// </summary>
    public double OrientationTargeted { get; private set; }

    /// <summary>
    /// The bot's targeted velocity regarding the y-dimension.
    /// </summary>
    public double YVelocityTargeted { get; private set; }

    /// <summary>
    /// The current target of the bot. (x-coordinate)
    /// </summary>
    public double XTarget { get; internal set; }

    /// <summary>
    /// The current target of the bot. (y-coordinate)
    /// </summary>
    public double YTarget { get; internal set; }

    /// <summary>
    /// The bot will evade when seeing something within this distance.
    /// </summary>
    public double EvadeDistance { get; internal set; }

    /// <summary>
    /// Frustration is used to adapt the speed of the bot.
    /// </summary>
    private double _frustration; // 0.0 -> 1.0 for maximal frustration

    /// <summary>
    /// Counts the number of consecutive moments (updates) being stuck.
    /// </summary>
    private int _stuckCount;

    /// <summary>
    /// The square-root of two (precalculated).
    /// </summary>
    private static readonly double _sqrt2 = Math.Sqrt(2.0);

    /// <summary>
    /// The predefined evasion state.
    /// </summary>
    private readonly BotEvade _botEvade;

    /// <summary>
    /// The current waypoint to go to.
    /// </summary>
    private Waypoint _currentWaypoint;

    /// <summary>
    /// The previous waypoint the bot came from.
    /// </summary>
    private Waypoint _previousWaypoint;

    /// <summary>
    /// The semaphores this bot is currently associated with.
    /// </summary>
    private readonly HashSet<QueueSemaphore> _enteredSemaphores = [];

    /// <summary>
    /// The elevators this bot is currently associated with.
    /// </summary>
    private readonly HashSet<Elevator> _enteredElevators = [];

    /// <summary>
    /// The current waypoint this bot is headed.
    /// </summary>
    public override Waypoint CurrentWaypoint
    {
        get => _currentWaypoint;
        set
        {
            // Signal the entrance to the guard and a possible elevator
            if (_currentWaypoint != null)
            {
                IEnumerable<QueueSemaphore> semaphoresPassed; Elevator elevatorEntered;
                _currentWaypoint.Pass(value, this, out semaphoresPassed, out elevatorEntered);
                // Store the passed semaphores to ensure the release of the resource the bot blocks even when the bot does not use a guarded connection when exiting the queue area
                if (semaphoresPassed != null)
                    foreach (var semaphore in semaphoresPassed)
                    {
                        if (_enteredSemaphores.Contains(semaphore))
                            _enteredSemaphores.Remove(semaphore);
                        else
                            _enteredSemaphores.Add(semaphore);
                    }
                // Store the entered elevator to ensure the release of it after exiting
                if (elevatorEntered != null)
                    _enteredElevators.Add(elevatorEntered);
            }

            // Release any possible semaphores that are still reserved by this bot although it is not in a queue area
            if (value != null && !value.IsQueueWaypoint && _enteredSemaphores.Any())
            {
                foreach (var semaphore in _enteredSemaphores)
                    semaphore.Release(this);
                _enteredSemaphores.Clear();
            }
            // Release any possible elevator that was entered
            if (value != null && (_previousWaypoint == null || !(_previousWaypoint.Elevator != null)) && value.Elevator == null && _enteredElevators.Any())
            {
                foreach (var elevator in _enteredElevators)
                {
                    elevator.InUse = false;
                    elevator.usedBy = null;
                }
                _enteredElevators.Clear();
            }

            // Manage waypoint history
            value?.AddBotApproaching(this);
            _currentWaypoint?.TraverseBot(this);
            _previousWaypoint?.RemoveBotLeaving(this);

            // Set new waypoints
            _previousWaypoint = _currentWaypoint;
            _currentWaypoint = value;
        }
    }

    #endregion

    #region core

    /// <summary>
    /// Discards the lock on the waypoint the bot is currently coming from.
    /// </summary>
    public void FreePreviousWaypoint()
    {
        if (_previousWaypoint != null)
        {
            _previousWaypoint.RemoveBotApproaching(this);
            _previousWaypoint.RemoveBotLeaving(this);
            //_previousWaypoint = null;
        }

        _currentWaypoint?.TraverseBot(this);
    }

    /// <summary>
    /// Idle is called whenever the <code>StateQueue</code> is empty, and should attempt to allocate tasks for the podbot
    /// </summary>
    public void Idle(double lastTime, double currentTime)
    {
        Instance.Controller.BotManager.TaskComplete(this, CurrentTask);
        Instance.Controller.BotManager.RequestNewTask(this);
        if (StateQueue.Any())
            StateQueue.First().Act(this, lastTime, currentTime);
    }

    /// <summary>
    /// Returns the largest approximate gap to escape. 
    /// 
    /// This gap is approximated by finding the gaps between all potential collideable objects around the Podbot,
    /// and weighting them positively by size and weighting them inversely by both distance and gapsize.
    /// The direction bisecting the largest of these weighted gaps is returned. 
    /// </summary>
    /// <param name="visibleDistance">The distance defining how far the bot can see.</param>
    /// <returns>The best direction to evade.</returns>
    public double GetBestEvadeDirection(double visibleDistance)
    {
        // Get visible objects within the distance of the next planned update
        var visibleObjects = new List<Circle>(Tier.GetBotsWithinDistance(X, Y, visibleDistance));
        if (Pod != null)
            visibleObjects.AddRange(Tier.GetPodsWithinDistance(X, Y, visibleDistance));

        var objects = new List<CollideableObject>();
        // Get object distances and directions
        foreach (var c in visibleObjects)
        {
            if (c == this || c == Pod)
                continue;

            var objectDirection = Math.Atan2(c.Y - Y, c.X - X);
            objects.Add(new CollideableObject(2 * c.Radius, GetDistance(c.X, c.Y), objectDirection));
        }

        // Add 4 walls
        if (X < visibleDistance)
            objects.Add(new CollideableObject(2 * Math.Sqrt(visibleDistance * visibleDistance - X * X), X, Math.PI));
        if (Tier.Length - X < visibleDistance)
            objects.Add(new CollideableObject(2 * Math.Sqrt(visibleDistance * visibleDistance - (Tier.Length - X) * (Tier.Length - X)), Tier.Length - X, 0.0));

        if (Y < visibleDistance)
            objects.Add(new CollideableObject(2 * Math.Sqrt(visibleDistance * visibleDistance - Y * Y), Y, 3 * Math.PI / 2));
        if (Tier.Width - Y < visibleDistance)
            objects.Add(new CollideableObject(2 * Math.Sqrt(visibleDistance * visibleDistance - (Tier.Width - Y) * (Tier.Width - Y)), Tier.Width - Y, Math.PI / 2));

        // If no objects, then nothing to collide with
        if (!objects.Any())
            return Orientation;

        // Sort objects by direction, so between each two objects is the smallest real gaps
        objects = objects.OrderBy(k => k).ToList();

        // Add beginning one again to complete the circle around the podbot
        objects.Add(new CollideableObject(objects.First().Size, objects.First().Distance, objects.First().Direction + 2 * Math.PI));

        // Find maximal gap - start with first gap
        var evadeDirection = (objects[0].Direction + objects[1].Direction) / 2;
        var weight = (objects[0].Size + objects[1].Size) / 2
                                                         / ((objects[0].Distance + objects[1].Distance) / 2
                                                            * (objects[1].Direction - objects[0].Direction));

        // For all after the first gap
        for (var i = 1; i < objects.Count - 1; i++)
        {
            // Get weight (a heuristic for speed)
            var w = (objects[i].Size + objects[i + 1].Size) / 2
                                                            / ((objects[i].Distance + objects[i + 1].Distance) / 2
                                                               * (objects[i + 1].Direction - objects[i].Direction));
            // If better direction, then choose direction bisecting the two objects
            if (w < weight)
            {
                weight = w;
                evadeDirection = (objects[i].Direction + objects[i + 1].Direction) / 2;
            }
        }
        return evadeDirection;
    }


    /// <summary>
    /// Returns the currently targeted speed.
    /// </summary>
    /// <returns>The targeted speed.</returns>
    public double GetTargetSpeed() { return Math.Sqrt(XVelocityTargeted * XVelocityTargeted + YVelocityTargeted * YVelocityTargeted); }

    /// <summary>
    /// Returns the difference of the current speed and the targeted one.
    /// </summary>
    /// <returns>The difference between the targeted speed and the current one.</returns>
    public double GetTargetSpeedDifference()
    {
        return Math.Sqrt((XVelocityTargeted - XVelocity) * (XVelocityTargeted - XVelocity) + (YVelocityTargeted - YVelocity) * (YVelocityTargeted - YVelocity));
    }

    /// <summary>
    /// Returns the difference of the current orientation and the targeted one.
    /// </summary>
    /// <returns>The difference between the targeted orientation and the current one.</returns>
    public double GetTargetOrientationDifference()
    {
        //Minimum of the two possible turning Directions
        return GetAbsoluteOrientationDifference(Orientation, OrientationTargeted);
    }

    /// <summary>
    /// Sets the targeted speed.
    /// </summary>
    /// <param name="speed">The new targeted speed.</param>
    public void SetTargetSpeed(double speed)
    {
        // Clamp to allowed values
        if (speed > MaxVelocity)
            speed = MaxVelocity;
        else if (speed < 0)
            speed = 0;

        XVelocityTargeted = speed * Math.Cos(GetInfoOrientation());
        YVelocityTargeted = speed * Math.Sin(GetInfoOrientation());

        // Find out how long it will take until the desired speed is reached
        if (Instance.SettingConfig.UseAcceleration)
            AccelerateUntil = CurrentTime + GetTargetSpeedDifference() / MaxAcceleration;
        else
            AccelerateUntil = CurrentTime;
    }

    /// <summary>
    /// Sets the targeted orientation.
    /// </summary>
    /// <param name="orientation">The new targeted orientation.</param>
    public void SetTargetOrientation(double orientation)
    {
        OrientationTargeted = (orientation + 2 * Math.PI) % (2 * Math.PI);

        // Find out how long it will take until the desired speed is reached
        if (Instance.SettingConfig.UseTurnDelay)
        {
            //TurnSpeed / (Math.PI * 2) = Time taken for a turn of exaclty one radian
            TurnUntil = CurrentTime + GetTargetOrientationDifference() / (Math.PI * 2 / TurnSpeed);
        }
        else
        {
            TurnUntil = CurrentTime;

            // Set orientation
            Orientation = OrientationTargeted;

            // Also set orientation for the carried pod
            if (Pod != null && Instance.SettingConfig.RotatePods) { Pod.Orientation = Orientation; }
        }
    }

    /// <summary>
    /// Logs the data of an unfinished trip.
    /// </summary>
    internal override void LogIncompleteTrip()
    {
        if (_stateQueue.Count != 0 && _stateQueue.First() is BotMove)
            (_stateQueue.First() as BotMove)?.LogUnfinishedTrip(this);
    }

    #endregion

    #region internal class
    /// <summary>
    /// <code>CollideableObject</code> is used to sort objects a bot can potentially collide with in order to generate a ring of these objects.
    /// </summary>
    private class CollideableObject(double s, double dist, double dir)
        : IComparer<CollideableObject>, IComparable<CollideableObject>
    {
        internal readonly double Size = s;
        internal readonly double Distance = dist;
        internal readonly double Direction = dir;

        #region IComparer<CollideableObject> Members

        public int Compare(CollideableObject x, CollideableObject y)
        {
            var dir1 = x.Direction;
            var dir2 = y.Direction;
            if (dir1 > dir2) return 1;
            if (dir2 > dir1) return -1;
            return 0;
        }

        #endregion

        #region IComparable<CollideableObject> Members

        public int CompareTo(CollideableObject other)
        {
            var dir2 = other.Direction;
            if (Direction > dir2) return 1;
            if (dir2 > Direction) return -1;
            return 0;
        }

        #endregion
    }
    #endregion

    #region optimization
    /// <summary>
    /// Used to store the search tree of A*.
    /// </summary>
    private class WaypointSearchData
    {
        public WaypointSearchData(double distanceTraveled, double distanceToGoal, Waypoint waypoint, WaypointSearchData parentMove, int depth, bool blockedApproaching, bool blockedLeaving)
        {
            DistanceTraveled = distanceTraveled; DistanceToGoal = distanceToGoal; Waypoint = waypoint; ParentMove = parentMove; Depth = depth; BlockedApproaching = blockedApproaching; BlockedLeaving = blockedLeaving;
        }
        public readonly double DistanceTraveled;
        public readonly double DistanceToGoal;
        public readonly Waypoint Waypoint;
        public readonly int Depth;
        public bool BlockedApproaching;
        public bool BlockedLeaving;
        public readonly WaypointSearchData ParentMove;
    }

    /// <summary>
    /// Used to store the result of a path search.
    /// </summary>
    private class WaypointSearchResult
    {
        /// <summary>
        /// The calculated travel route.
        /// </summary>
        internal readonly LinkedList<Waypoint> Route = [];
        /// <summary>
        /// Indicates whether there is a next step available in the route (assuming that the bot is currently at the first position of the route in the list).
        /// </summary>
        public bool HasNextStep => Route.Count >= 2;

        /// <summary>
        /// The next waypoint to go to when traveling this route (assuming the bot is currently located at the first position stored in this route).
        /// </summary>
        public Waypoint NextStep => Route.First.Next.Value;

        /// <summary>
        /// The current node the bot is positioned.
        /// </summary>
        public Waypoint First => Route.First.Value;

        /// <summary>
        /// Indicates whether the destination of the route was reached.
        /// </summary>
        public bool IsFinished => Route.Count <= 1;

        /// <summary>
        /// Indicates whether the next waypoint of the route is dynamically blocked by other bots. If there is no next waypoint this always returns <code>false</code>.
        /// </summary>
        public bool IsNextStepBlocked => Route.Count <= 1 ? false : Route.First.Next.Value.Elevator?.InUse ?? Route.First.Next.Value.AnyBotsOverall;

        /// <summary>
        /// Indicates whether there are currently bots approaching the waypoint after the next step's waypoint.
        /// </summary>
        public bool IsDepth2Approaching => Route.Count < 3 ? false : Route.First.Next.Next.Value.AnyBotsApproaching;

        /// <summary>
        /// Stores another step to this route. The steps have to be added in backwards order.
        /// </summary>
        /// <param name="waypoint">The waypoint of the route to add.</param>
        public void AddStep(Waypoint waypoint) { Route.AddFirst(waypoint); }
        /// <summary>
        /// Performs a step on this route by discarding the first waypoint in this route.
        /// </summary>
        public void Step() { Route.RemoveFirst(); }
    }

    /// <summary>
    /// Uses A* to find the next move to make in order to get from start to end.
    /// </summary>
    /// <param name="startNode">The starting waypoint.</param>
    /// <param name="destinationNode">The destination waypoint.</param>
    /// <returns>A waypoint connected to the start waypoint that belongs to the best path from start to end, defining the next move with additional meta information.</returns>
    private WaypointSearchResult GetNextWaypointTo(Waypoint startNode, Waypoint destinationNode)
    {
        // Measure time for decision
        var before = DateTime.Now;
        // Generate the path
        var result = PlanPath(startNode, destinationNode);
        // Calculate decision time
        Instance.Observer.TimePathPlanning((DateTime.Now - before).TotalSeconds);
        // Return it
        return result;
    }

    /// <summary>
    /// Uses A* to find the next move to make in order to get from start to end.
    /// </summary>
    /// <param name="startNode">The starting waypoint.</param>
    /// <param name="destinationNode">The destination waypoint.</param>
    /// <returns>A waypoint connected to the start waypoint that belongs to the best path from start to end, defining the next move with additional meta information.</returns>
    private WaypointSearchResult PlanPath(Waypoint startNode, Waypoint destinationNode)
    {
        if (startNode == null || destinationNode == null)
            return null;

        var openLocations = new Dictionary<Waypoint, WaypointSearchData>();
        var closedLocations = new Dictionary<Waypoint, WaypointSearchData>();
        openLocations[startNode] = new WaypointSearchData(0.0, startNode.GetDistance(destinationNode), startNode, null, 0, false, false);

        // Don't move if already at destination
        if (startNode == destinationNode)
            return null;

        // Maximum number of waypoints to look at in search
        var maxNumIterations = 3000; // TODO this does not make any sense
        var numIterations = 0;

        // Loop until end is found
        while (true)
        {
            // Find lowest cost waypoint in openLocations
            Waypoint currentNode = null;
            var lowestCost = double.PositiveInfinity;
            foreach (var w in openLocations.Keys)
            {
                if (openLocations[w].DistanceTraveled + openLocations[w].DistanceToGoal < lowestCost)
                {
                    currentNode = w;
                    lowestCost = openLocations[w].DistanceTraveled + openLocations[w].DistanceToGoal;
                }
            }
            // Something wrong happened -can't find the end
            if (currentNode == null)
                return null;

            // Grab the details about the current waypoint
            var currentNodeData = openLocations[currentNode];

            // If the closest is also the destination or out of iterations
            if (currentNode == destinationNode || numIterations++ == maxNumIterations)
            {
                // Init result
                var result = new WaypointSearchResult();
                // Found it on the first move
                if (currentNodeData.ParentMove == null)
                    return null;
                // Go back to the first move made
                while (currentNodeData != null)
                {
                    result.AddStep(currentNodeData.Waypoint);
                    currentNodeData = currentNodeData.ParentMove;
                }
                return result;
            }

            // Transfer closest from open to closed list
            closedLocations[currentNode] = openLocations[currentNode];
            openLocations.Remove(currentNode);

            // Expand all the moves
            foreach (var successorNode in currentNode.Paths)
            {
                // Check whether the node is already on closed
                if (closedLocations.ContainsKey(successorNode))
                    // Don't deal with anything already on the closed list (don't want loops)
                    continue;

                // Can't go through a pod storage location if carrying a pod, unless it's the destination
                if (successorNode.PodStorageLocation && Pod != null && successorNode != destinationNode)
                    continue;

                // Can't go through the node if it is dynamically blocked at the moment
                if (!currentNode.IsAccessible(successorNode))
                    continue;

                // Ignore the waypoint if another bot is already registered to it (if waiting is enabled for path planning)
                if (Config.SimpleWaitingEnabled && successorNode.AnyBotsOverall && startNode == currentNode)
                    continue;

                // Tag on more distance for a crowded node, as long as it's not the end node
                double additionalDistance = 0;
                if (successorNode != destinationNode)
                    additionalDistance = 5 * 2 * Radius * successorNode.BotCountOverall;

                // Tag on more distance for a node on the wrong level
                if (successorNode.Tier != destinationNode.Tier)
                    additionalDistance += Instance.WrongTierPenaltyDistance;

                // If it's not in the open list, add it
                if (!openLocations.ContainsKey(successorNode))
                {
                    openLocations[successorNode] =
                        new WaypointSearchData(
                            currentNodeData.DistanceTraveled + currentNode[successorNode], // The distance already traveled
                            successorNode.GetDistance(destinationNode) + additionalDistance, // The approximate distance to the goal
                            successorNode,  // The node itself
                            currentNodeData, // Parent data 
                            currentNodeData.Depth + 1, // The current depth
                            successorNode.AnyBotsApproaching, // Indicate if the node is blocked by an approaching bot
                            successorNode.AnyBotsLeaving); // Indicate if the node is blocked by a leaving bot
                }
                else
                {
                    // It's already in the open list, but see if this new path is better
                    var oldPath = openLocations[successorNode];
                    // Replace it with the new one
                    if (oldPath.DistanceTraveled + oldPath.DistanceToGoal > currentNodeData.DistanceTraveled + currentNode[successorNode])
                        openLocations[successorNode] =
                            new WaypointSearchData(
                                currentNodeData.DistanceTraveled + currentNode[successorNode], // The distance already traveled
                                successorNode.GetDistance(destinationNode) + additionalDistance, // The approximate distance to the goal
                                successorNode, // The node itself
                                currentNodeData, // Parent data
                                currentNodeData.Depth + 1, // The current depth
                                successorNode.AnyBotsApproaching, // Indicate if the node is blocked by an approaching bot
                                successorNode.AnyBotsLeaving); // Indicate if the node is blocked by a leaving bot
                }
            }
        }
    }

    #endregion

    #region Bot Members
    /// <summary>
    /// Assigns a new task to the bot.
    /// </summary>
    /// <param name="t">The task to execute.</param>
    public override void AssignTask(BotTask t)
    {
        // Abort old task
        CurrentTask = t;
        ClearStates();
        if (t.Type == BotTaskType.None)
            return;

        switch (t.Type)
        {
            case BotTaskType.None:
            {
                // Do nothing
            }
                break;
            case BotTaskType.ParkPod:
            {
                var storePodTask = t as ParkPodTask;
                // If we are carrying a pod and it does not match the one of the task we cannot execute the task
                if (storePodTask.Pod != Pod)
                {
                    Instance.Controller.BotManager.TaskAborted(this, storePodTask);
                    return;
                }
                // Add states for parking the pod
                EnqueueState(new BotMove(this, storePodTask.StorageLocation));
                EnqueueState(new BotSetdownPod(storePodTask.StorageLocation));
            }
                break;
            case BotTaskType.RepositionPod:
            {
                var repositionTask = t as RepositionPodTask;
                // Check whether we already have a pod
                if (Pod == null)
                {
                    // Add states for fetching pod
                    EnqueueState(new BotMove(this, repositionTask.Pod.Waypoint));
                    EnqueueState(new BotPickupPod(repositionTask.Pod));
                    // Add states for parking pod
                    EnqueueState(new BotMove(this, repositionTask.StorageLocation));
                    EnqueueState(new BotSetdownPod(repositionTask.StorageLocation));
                    // Log a repositioning move
                    Instance.NotifyRepositioningStarted(this, repositionTask.Pod.Waypoint, repositionTask.StorageLocation, repositionTask.Pod);

                }
                // We are already carrying a pod: we cannot execute the task
                else
                {
                    Instance.Controller.BotManager.TaskAborted(this, repositionTask);
                    return;
                }

            }
                break;
            case BotTaskType.Insert:
            {
                var storeTask = t as InsertTask;
                if (storeTask.ReservedPod != Pod)
                {
                    EnqueueState(new BotMove(this, storeTask.ReservedPod.Waypoint));
                    EnqueueState(new BotPickupPod(storeTask.ReservedPod));
                }
                EnqueueState(new BotMove(this, storeTask.InputStation.Waypoint));
                EnqueueState(new BotGetItems(storeTask));
            }
                break;
            case BotTaskType.Extract:
            {
                var extractTask = t as ExtractTask;
                if (extractTask.ReservedPod != Pod)
                {
                    EnqueueState(new BotMove(this, extractTask.ReservedPod.Waypoint));
                    EnqueueState(new BotPickupPod(extractTask.ReservedPod));
                }
                EnqueueState(new BotMove(this, extractTask.OutputStation.Waypoint));
                EnqueueState(new BotPutItems(extractTask));
            }
                break;
            case BotTaskType.Rest:
            {
                EnqueueState(new BotMove(this, (t as RestTask)?.RestingLocation));
                EnqueueState(new BotRest((t as RestTask)?.RestingLocation));
            }
                break;
            default:
                throw new ArgumentException("Unknown task-type: " + t.Type);
        }

        // Track task count
        StatAssignedTasks++;
        StatTotalTaskCounts[t.Type]++;
    }

    /// <summary>
    /// Blocks the robot for the specified time.
    /// </summary>
    /// <param name="time">The time to be blocked for.</param>
    public override void WaitUntil(double time)
    {
        BlockedUntil = Math.Max(time, BlockedUntil);
    }

    #endregion

    #region IUpdateable Members

    /// <summary>
    /// Last Update Time of the bot.
    /// </summary>
    public double CurrentTime = -1.0;
    /// <summary>
    /// Moment when the bot finishes accelerating.
    /// </summary>
    public double AccelerateUntil = -1.0;
    /// <summary>
    /// Moment when the bot finishes cruising.
    /// </summary>
    public double CruiseUntil = -1.0;
    /// <summary>
    /// Moment when the bot finishes turning.
    /// </summary>
    public double TurnUntil = -1.0;
    /// <summary>
    /// Minimum of all until-times.
    /// </summary>
    public double MinUntil;

    /// <summary>
    /// The next event when this element has to be updated.
    /// </summary>
    /// <param name="currentTime">The current time of the simulation.</param>
    /// <returns>The next time this element has to be updated.</returns>
    public override double GetNextEventTime(double currentTime)
    {
        // If not waiting on anything, then not planning on moving in the near future - only if bot has nothing to do
        if (currentTime >= BlockedUntil && currentTime >= AccelerateUntil && currentTime >= CruiseUntil && currentTime >= TurnUntil)
        {
            return Double.PositiveInfinity;
        }

        // Return soonest event that has not happened yet
        MinUntil = Double.PositiveInfinity;
        if (BlockedUntil > currentTime) MinUntil = Math.Min(BlockedUntil, MinUntil);
        if (AccelerateUntil > currentTime) MinUntil = Math.Min(AccelerateUntil, MinUntil);
        if (CruiseUntil > currentTime) MinUntil = Math.Min(CruiseUntil, MinUntil);
        if (TurnUntil > currentTime) MinUntil = Math.Min(TurnUntil, MinUntil);
        return MinUntil;
    }
    /// <summary>
    /// Updates the element to the specified time.
    /// </summary>
    /// <param name="lastTime">The time before the update.</param>
    /// <param name="currentTime">The time to update to.</param>
    public override void Update(double lastTime, double currentTime)
    {
        if (currentTime < BlockedUntil)
            return;

        CurrentTime = currentTime;
        var timeDelta = currentTime - lastTime;

        // Remember old position
        var xOld = X; var yOld = Y;

        // Indicate change at instance
        Instance.Changed = true;

        // Make sure tolerance is less than the map's tolerance/3 to make sure that if it sets a pod down,
        // the next one bot will be able to pick it up (and could also be tolerance/3 away)
        // better fudge factor than tolerance / 2 (which is the minimum that will work)
        var tolerance = Instance.SettingConfig.Tolerance / 3;

        if (GetTargetOrientationDifference() > tolerance * tolerance)
            _updateTurnBot(timeDelta);
        else
            _updateMoveBot(timeDelta);

        // Act on current state if any, otherwise idle
        if (StateQueue.Any())
            StateQueue.First().Act(this, lastTime, currentTime);
        else
            Idle(lastTime, currentTime);

        // Update statistics
        _updateStatistics(timeDelta, xOld, yOld);
    }

    /// <summary>
    /// turn the bot to the targeted orientation
    /// </summary>
    /// <param name="timeDelta">time since last update</param>
    private void _updateTurnBot(double timeDelta)
    {
        //stop the pod (this should already be 0)
        XVelocity = YVelocity = 0;

        var timeLeftUntilTargetOrientation = GetTargetOrientationDifference() / (Math.PI * 2 / TurnSpeed);

        //the bot will be reach the target within this update
        if (timeLeftUntilTargetOrientation <= timeDelta)
        {
            Orientation = OrientationTargeted;

            //use the rest of the time to move
            _updateMoveBot(timeDelta - timeLeftUntilTargetOrientation);
        }
        else
        {
            var orientationChange = timeDelta * (Math.PI * 2 / TurnSpeed);

            //check the rotation direction
            if (GetOrientationDifference(Orientation, OrientationTargeted) < 0)
                Orientation -= orientationChange; //turn counterclockwise
            else
                Orientation += orientationChange; //turn clockwise
        }

        //set the pod orientation
        if (Pod != null && Instance.SettingConfig.RotatePods)
            Pod.Orientation = Orientation;

    }

    /// <summary>
    /// accelerate the bot to the targetet velocity
    /// </summary>
    /// <param name="timeDelta">time since last update</param>
    private void _updateMoveBot(double timeDelta)
    {
        //drive ahead orientation targeted
        Orientation = OrientationTargeted;

        // Use old velocities to clamp acceleration
        var oldXVelocity = XVelocity;
        var oldYVelocity = YVelocity;

        // Set new velocity
        XVelocity = XVelocityTargeted;
        YVelocity = YVelocityTargeted;

        // Clamp velocity
        if (XVelocity * XVelocity + YVelocity * YVelocity > MaxVelocity * MaxVelocity)
        {
            var velocityMagnitude = Math.Sqrt(XVelocity * XVelocity + YVelocity * YVelocity);
            XVelocity = MaxVelocity * (XVelocity / velocityMagnitude);
            YVelocity = MaxVelocity * (YVelocity / velocityMagnitude);
        }

        if (Instance.SettingConfig.UseAcceleration)
        {
            // Clamp acceleration
            var xAccel = (XVelocity - oldXVelocity) / timeDelta;
            var yAccel = (YVelocity - oldYVelocity) / timeDelta;
            if (xAccel * xAccel + yAccel * yAccel > MaxAcceleration * MaxAcceleration)
            {
                var accelMagnitude = Math.Sqrt(xAccel * xAccel + yAccel * yAccel);
                XVelocity = oldXVelocity + MaxAcceleration * timeDelta * (xAccel / accelMagnitude);
                YVelocity = oldYVelocity + MaxAcceleration * timeDelta * (yAccel / accelMagnitude);
            }
        }

        // Calculate movement
        var xNew = XVelocity * timeDelta + X;
        var yNew = YVelocity * timeDelta + Y;

        // Try to make move. If can'task move due to a collision, then stop
        if (Instance.Compound.BotCurrentTier[this].MoveBot(this, xNew, yNew)) return;
        XVelocity = 0.0; YVelocity = 0.0;
        XVelocityTargeted = 0.0; YVelocityTargeted = 0.0;
        BlockedUntil = CurrentTime + CollisionPenaltyTime;
        // Log collision
        StatNumCollisions++;
        Instance.NotifyCollision(this, Tier);
    }

    private void _updateStatistics(double timeDelta, double xOld, double yOld)
    {
        // Measure moving time
        if (Moving)
            StatTotalTimeMoving += timeDelta;
        // Measure queueing time
        if (IsQueueing)
            StatTotalTimeQueueing += timeDelta;

        // Set moving flags
        if (XVelocity == 0.0 && YVelocity == 0.0)
            Moving = false;
        else
            Moving = true;
        Pod?.Moving = Moving;

        // Count distanceTraveled
        StatDistanceTraveled += Math.Sqrt((X - xOld) * (X - xOld) + (Y - yOld) * (Y - yOld));

        // Compute time in previous task
        StatTotalTaskTimes[StatLastTask] += timeDelta;
        StatLastTask = CurrentTask?.Type ?? BotTaskType.None;

        // Measure time spent in state
        StatTotalStateTimes[StatLastState] += timeDelta;
        StatLastState = _stateQueue.Count > 0 ? _stateQueue.First().Type : BotStateType.Rest;
    }

    #endregion

    #region IBotInfo Members

    /// <summary>
    /// Gets the x-position of the goal of the bot.
    /// </summary>
    /// <returns>The x-position.</returns>
    public override double GetInfoGoalX() { return XTarget; }
    /// <summary>
    /// Gets the y-position of the goal of the bot.
    /// </summary>
    /// <returns>The y-position.</returns>
    public override double GetInfoGoalY() { return YTarget; }
    /// <summary>
    /// Gets the target orientation in radians. (An element facing east is defined with orientation 0 or equally 2*pi.)
    /// </summary>
    /// <returns>The orientation.</returns>
    public override double GetInfoTargetOrientation() { return OrientationTargeted; }
    /// <summary>
    /// Returns the current state the bot is in.
    /// </summary>
    /// <returns>The active state.</returns>
    public override string GetInfoState() { return _currentStateName; }
    /// <summary>
    /// Gets the current waypoint that is considered by planning.
    /// </summary>
    /// <returns>The current waypoint.</returns>
    public override IWaypointInfo GetInfoCurrentWaypoint() { return CurrentWaypoint; }
    /// <summary>
    /// Gets the destination of the bot.
    /// </summary>
    /// <returns>The destination.</returns>
    public override IWaypointInfo GetInfoDestinationWaypoint() { return _currentDestinationWP ?? _currentWaypoint; }
    /// <summary>
    /// Gets the goal of the bot.
    /// </summary>
    /// <returns>The goal.</returns>
    public override IWaypointInfo GetInfoGoalWaypoint() { return null; }
    /// <summary>
    /// The current path the bot is following.
    /// </summary>
    private List<IWaypointInfo> _currentPath = [];
    /// <summary>
    /// Gets the current path of the bot.
    /// </summary>
    /// <returns>The current path.</returns>
    public override List<IWaypointInfo> GetInfoPath() { return _currentPath; }
    /// <summary>
    /// Indicates whether the robot is currently blocked.
    /// </summary>
    /// <returns><code>true</code> if the robot is blocked, <code>false</code> otherwise.</returns>
    public override bool GetInfoBlocked() { return Instance.Controller.CurrentTime < BlockedUntil; }
    /// <summary>
    /// The time until the bot is blocked.
    /// </summary>
    /// <returns>The time until the bot is blocked.</returns>
    public override double GetInfoBlockedLeft()
    {
        var blockedUntil = BlockedUntil; var currentTime = Instance.Controller.CurrentTime;
        return blockedUntil > currentTime ? blockedUntil - currentTime : double.NaN;
    }
    /// <summary>
    /// Indicates whether the bot is currently queueing in a managed area.
    /// </summary>
    /// <returns><code>true</code> if the robot is within a queue area, <code>false</code> otherwise.</returns>
    public override bool GetInfoIsQueueing() { return IsQueueing; }

    #endregion

    #region State handling

    /// <summary>
    /// The state-queue containing all the operations to execute consecutively.
    /// </summary>
    private readonly LinkedList<IBotState> _stateQueue = [];

    /// <summary>
    /// The state-queue containing all the operations to execute consecutively.
    /// </summary>
    public IEnumerable<IBotState> StateQueue => _stateQueue;

    /// <summary>
    /// The state after the current state.
    /// </summary>
    public IBotState NextState { get
    {
        if (_stateQueue.Count > 1) return _stateQueue.First.Next.Value;
        return null;
    } }

    /// <summary>
    /// Enqueues a new state at the end of the queue.
    /// </summary>
    /// <param name="state">The state to add.</param>
    public void EnqueueState(IBotState state) { _stateQueue.AddLast(state); UpdateMetaInfo(); }

    /// <summary>
    /// Cuts the queue and adds a new state at the beginning.
    /// </summary>
    /// <param name="state">The new state to add.</param>
    public void CutState(IBotState state) { _stateQueue.AddFirst(state); UpdateMetaInfo(); }

    /// <summary>
    /// Removes the current state from the queue.
    /// </summary>
    public void DequeueState() { _stateQueue.RemoveFirst(); UpdateMetaInfo(); }

    /// <summary>
    /// Clears all states currently in the queue.
    /// </summary>
    public void ClearStates() { _stateQueue.Clear(); UpdateMetaInfo(); }

    /// <summary>
    /// The current destination of the bot as useful information for other mechanisms.
    /// </summary>
    internal override Waypoint TargetWaypoint => _currentDestinationWP;

    private string _currentStateName = "";

    private Waypoint _currentDestinationWP;

    private void UpdateMetaInfo()
    {
        if (_stateQueue.Any())
            _currentStateName = _stateQueue.First().ToString();
        if (_stateQueue.Any())
            _currentDestinationWP = _stateQueue.FirstOrDefault().DestinationWaypoint;
        else
            _currentDestinationWP = null;
    }

    #endregion

    #region State definitions

    #region Pickup and Setdown states

    /// <summary>
    /// The state defining the operation of picking up a pod at the current location.
    /// </summary>
    internal class BotPickupPod : IBotState
    {
        public override string ToString() { return "PickupPod"; }
        public BotStateType Type => BotStateType.PickupPod;
        private readonly Pod _pod;
        private bool _initialized;
        private bool _executed;
        public BotPickupPod(Pod b) { _pod = b; DestinationWaypoint = _pod.Waypoint; }
        public Waypoint DestinationWaypoint { get; }

        public void Act(Bot self, double lastTime, double currentTime)
        {
            var driver = self as BotHazard;

            // Initialize
            if (!_initialized) { self.StatTotalStateCounts[Type]++; _initialized = true; }

            // Dequeue the state as soon as it is finished
            if (_executed && driver.CurrentTime >= driver.BlockedUntil)
            {
                driver.DequeueState();
                return;
            }
            // Act based on whether pod was picked up
            if (driver.PickupPod(_pod, driver.CurrentTime))
            {
                _executed = true;
                var podWaypoint = _pod.Waypoint;
                driver.Instance.WaypointGraph.PodPickup(_pod);
                driver.Instance.Controller.BotManager.PodPickedUp(driver, _pod, podWaypoint);
            }
            else
            {
                // Failed to pick up pod
                driver.ClearStates();
                driver.Instance.Controller.BotManager.TaskAborted(driver, driver.CurrentTask);
            }
        }
    }

    /// <summary>
    /// The state defining the operation of setting down a pod at the current location.
    /// </summary>
    internal class BotSetdownPod : IBotState
    {
        public override string ToString() { return "SetdownPod"; }
        public BotStateType Type => BotStateType.SetdownPod;
        private readonly Waypoint _waypoint;
        private bool _initialized;
        private bool _executed;
        public BotSetdownPod(Waypoint w) { _waypoint = w; }
        public Waypoint DestinationWaypoint => _waypoint;

        public void Act(Bot self, double lastTime, double currentTime)
        {
            var driver = self as BotHazard;

            // Initialize
            if (!_initialized) { self.StatTotalStateCounts[Type]++; _initialized = true; }

            // Dequeue the state as soon as it is finished
            if (_executed && currentTime >= driver.BlockedUntil)
            {
                driver.DequeueState();
                return;
            }
            // Get pod
            var b = driver.Pod;
            // Act based on whether pod was set down
            if (driver.SetdownPod(currentTime))
            {
                _executed = true;
                driver.Instance.WaypointGraph.PodSetdown(b, _waypoint);
                driver.Instance.Controller.BotManager.PodSetDown(driver, b, _waypoint);
                driver.DequeueState();
            }
            else
            {
                // Failed to set down pod
                driver.ClearStates();
                driver.Instance.Controller.BotManager.TaskAborted(driver, driver.CurrentTask);
            }
        }
    }

    #endregion

    #region Get and Put states

    /// <summary>
    /// The state defining the operation of storing an item-bundle in the pod at an input-station.
    /// </summary>
    internal class BotGetItems : IBotState
    {
        public override string ToString() { return "GetItems"; }
        public BotStateType Type => BotStateType.GetItems;
        private readonly InsertTask _storeTask;
        private bool _initialized;
        private bool alreadyRequested;
        public BotGetItems(InsertTask storeTask) { _storeTask = storeTask; DestinationWaypoint = _storeTask.InputStation.Waypoint; }
        public Waypoint DestinationWaypoint { get; }

        public void Act(Bot self, double lastTime, double currentTime)
        {
            var driver = self as BotHazard;

            // Initialize
            if (!_initialized) { self.StatTotalStateCounts[Type]++; _initialized = true; }

            // Make sure bot is in vacinity of station, otherwise go there
            /*
            if (_driver.GetDistance(_station.X, _station.Y) > _driver.Instance.Configuration.Tolerance)
            {
                _driver.CutState(new BotMove(_driver, _station.X, _station.Y));
                return;
            }*/

            // If it's the first time, request the bundles
            if (!alreadyRequested)
            {
                _storeTask.InputStation.RequestBundle(driver, _storeTask.Requests.First());
                alreadyRequested = true;
            }

            if (driver.Pod == null)
            {
                // Something wrong happened... don't have a pod!
                driver.Instance.Controller.BotManager.TaskAborted(driver, driver.CurrentTask);
                driver.DequeueState();
                if (driver.StateQueue.Any())
                    driver.StateQueue.First().Act(driver, lastTime, currentTime);
                return;
            }

            // See if bundle has been deposited in the pod
            switch (_storeTask.Requests.First().State)
            {
                case Management.RequestState.Unfinished: /* Ignore */ break;
                case Management.RequestState.Aborted: // Request was aborted for some reason - give it back to the manager for re-insertion
                {
                    // Remove the request that was just aborted
                    _storeTask.FirstAborted();
                    // See whether there are more bundles to store
                    if (_storeTask.Requests.Any())
                    {
                        // Store another one
                        alreadyRequested = false;
                    }
                    else
                    {
                        // We are done here
                        driver.DequeueState();
                        if (driver.StateQueue.Any())
                            driver.StateQueue.First().Act(driver, lastTime, currentTime);
                    }
                }
                    break;
                case Management.RequestState.Finished: // Request was finished - we can go on
                {
                    // Remove the request that was just completed
                    _storeTask.FirstStored();
                    // See whether there are more bundles to store
                    if (_storeTask.Requests.Any())
                    {
                        // Store another one
                        alreadyRequested = false;
                    }
                    else
                    {
                        // We are done here
                        driver.DequeueState();
                        if (driver.StateQueue.Any())
                            driver.StateQueue.First().Act(driver, lastTime, currentTime);
                    }
                }
                    break;
                default: throw new ArgumentException("Unknown request state: " + _storeTask.Requests.First().State);
            }
        }
    }

    /// <summary>
    /// The state defining the operation of picking an item from the pod at an output-station.
    /// </summary>
    internal class BotPutItems : IBotState
    {
        public override string ToString() { return "PutItems"; }
        public BotStateType Type => BotStateType.PutItems;
        private readonly ExtractTask _extractTask;
        private bool _initialized;
        private bool alreadyRequested;
        public BotPutItems(ExtractTask extractTask)
        { _extractTask = extractTask; DestinationWaypoint = _extractTask.OutputStation.Waypoint; }

        public Waypoint DestinationWaypoint { get; }

        public void Act(Bot self, double lastTime, double currentTime)
        {
            var driver = self as BotHazard;

            // Initialize
            if (!_initialized) { self.StatTotalStateCounts[Type]++; _initialized = true; }

            // Make sure bot is in vacinity of station, otherwise go there
            /*
            if (_driver.GetDistance(_station.X, _station.Y) > _driver.Instance.Configuration.Tolerance)
            {
                _driver.CutState(new BotMove(_driver, _station.X, _station.Y));
                return;
            }
             * */

            // If it's the first time, request the items be taken
            if (!alreadyRequested)
            {
                _extractTask.OutputStation.RequestItemTake(driver, _extractTask.Requests.First());
                alreadyRequested = true;
            }

            if (driver.Pod == null)
            {
                // Something wrong happened... don't have a pod!
                driver.Instance.Controller.BotManager.TaskAborted(driver, driver.CurrentTask);
                driver.DequeueState();
                if (driver.StateQueue.Any())
                    driver.StateQueue.First().Act(self, lastTime, currentTime);
                return;
            }

            // See if item has been picked from the pod
            switch (_extractTask.Requests.First().State)
            {
                case Management.RequestState.Unfinished: /* Ignore */ break;
                case Management.RequestState.Aborted: // Request was aborted for some reason - give it back to the manager for re-insertion
                {
                    // Remove the request that was just aborted
                    _extractTask.FirstAborted();
                    // See whether there are more items to pick
                    if (_extractTask.Requests.Any())
                    {
                        // Pick another one
                        alreadyRequested = false;
                    }
                    else
                    {
                        // We are done here
                        driver.DequeueState();
                        if (driver.StateQueue.Any())
                            driver.StateQueue.First().Act(driver, lastTime, currentTime);
                    }
                }
                    break;
                case Management.RequestState.Finished: // Request was finished - we can go on
                {
                    // Remove the request that was just completed
                    _extractTask.FirstPicked();
                    // See whether there are more items to pick
                    if (_extractTask.Requests.Any())
                    {
                        // Pick another one
                        alreadyRequested = false;
                    }
                    else
                    {
                        // We are done here
                        driver.DequeueState();
                        if (driver.StateQueue.Any())
                            driver.StateQueue.First().Act(driver, lastTime, currentTime);
                    }
                }
                    break;
                default: throw new ArgumentException("Unknown request state: " + _extractTask.Requests.First().State);
            }
        }
    }

    #endregion

    #region Rest state

    internal class BotRest : IBotState
    {
        // TODO make rest time randomized and parameterized
        public const double DEFAULT_REST_TIME = 10;

        private bool _initialized;
        public BotRest(Waypoint waypoint) { DestinationWaypoint = waypoint; }
        public Waypoint DestinationWaypoint { get; }

        #region IBotState Members

        public BotStateType Type => BotStateType.Rest;

        public override string ToString() { return "Rest"; }

        public void Act(Bot self, double lastTime, double currentTime)
        {
            var driver = self as BotHazard;

            // Initialize
            if (!_initialized) { self.StatTotalStateCounts[Type]++; _initialized = true; }

            // Randomly rest or exit resting
            if (self.Instance.Randomizer.NextDouble() < .9)
            {
                // Rest for a predefined period
                driver.BlockedUntil = driver.CurrentTime + DEFAULT_REST_TIME;
            }
            else
            {
                // Randomly exit the resting
                driver.DequeueState();
                if (driver.StateQueue.Any())
                    driver.StateQueue.First().Act(driver, lastTime, currentTime);
            }
        }

        #endregion
    }
    #endregion

    #region Move state

    /// <summary>
    /// The state defining the operation of moving.
    /// </summary>
    internal class BotMove : IBotState
    {
        public override string ToString() { return "Move"; }
        public BotStateType Type => BotStateType.Move;
        private readonly BotHazard _driver;
        /// <summary>
        /// Indicates whether it is the first time we execute the state.
        /// </summary>
        private bool _initialized;
        internal double moveToX, moveToY;
        public Waypoint DestinationWaypoint { get; }
        private WaypointSearchResult _currentRoute;
        public BotMove(BotHazard driver, double x, double y) { _driver = driver; DestinationWaypoint = null; _driver.CurrentWaypoint = null; moveToX = x; moveToY = y; }
        public BotMove(BotHazard driver, Waypoint w) { _driver = driver; DestinationWaypoint = w; moveToX = w?.X ?? _driver.X; moveToY = w?.Y ?? _driver.Y; }

        /// <summary>
        /// Logs an unfinished trip.
        /// </summary>
        /// <param name="bot">The bot that is logging the trip.</param>
        internal void LogUnfinishedTrip(BotHazard bot)
        {
            // Manage connectivity statistics
            DestinationWaypoint?.StatLogUnfinishedTrip(bot);
        }

        public bool needToEvade(Bot self)
        {
            var currentSpeed = _driver.GetSpeed();	//called once for code efficiency (since getSpeed does a sqrt)

            // Find possible distance to be covered before next planned update
            _driver.GetNextEventTime(_driver.CurrentTime);
            var timeInterval = Math.Max(_driver.MinUntil - _driver.CurrentTime, 0.05);
            var distanceToBeCovered = Math.Max(currentSpeed, _driver.GetTargetSpeed()) * timeInterval;
            distanceToBeCovered += 2 * Math.Max(_driver.GetInfoRadius(), _driver.Pod?.GetInfoRadius() ?? 0);	// Count its radius and the radius of another object
            // Make sure see at least a minimal distance regardless of speed
            distanceToBeCovered = Math.Max(distanceToBeCovered, _driver.EvadeDistance);

            // Get visible objects within the distance of the next planned update
            var visibleObjects = new List<Circle>(_driver.Tier.GetBotsWithinDistance(_driver.X, _driver.Y, distanceToBeCovered));
            if (_driver.Pod != null)
                visibleObjects.AddRange(_driver.Tier.GetPodsWithinDistance(_driver.X, _driver.Y, distanceToBeCovered));

            // Don't want to do anything yet, unless something is now visible other than the podbot itself (and pod if applicable) a collision occured (speed == 0)
            if ((_driver.Pod == null && visibleObjects.Count == 1) || (_driver.Pod != null && visibleObjects.Count == 2))
                return false;

            // Find closest object
            var minDist2 = Double.PositiveInfinity;	// Minimum distance squared
            foreach (var c in visibleObjects)
            {
                if (c == self || c == self.Pod)
                    continue;

                // See if infront of podbot 
                var objectDirection = Math.Atan2(c.Y - _driver.Y, c.X - _driver.X);
                var relativeDirection = GetOrientationDifference(objectDirection, _driver.Orientation);
                if (Math.Abs(relativeDirection) > Math.PI / 4 + 0.5)	// Just beyond 1/4 circle, to make sure they don't get stuck
                    continue;

                // See if it's closer than any other
                var dist2 = (_driver.X - c.X) * (_driver.X - c.X) + (_driver.Y - c.Y) * (_driver.Y - c.Y);
                if (dist2 < minDist2)
                    minDist2 = dist2;
            }

            // If anything is closer than this constant value, then evade...
            if (minDist2 < _driver.EvadeDistance * _driver.EvadeDistance)
            {
                var newDirection = _driver.GetBestEvadeDirection(_driver.EvadeDistance);

                if (_driver.Orientation != newDirection)
                    _driver.SetTargetOrientation(newDirection);

                _driver.SetTargetSpeed(_driver.MaxVelocity);
                _driver.CruiseUntil = _driver.Instance.SettingConfig.UseAcceleration ?
                    _driver.CurrentTime + (Math.Sqrt(2 * _driver.MaxAcceleration * _driver.Radius + _driver.MaxVelocity * _driver.MaxVelocity) - _driver.MaxVelocity) / _driver.MaxAcceleration / 2 :
                    _driver.CurrentTime + _driver.Radius / _driver.MaxVelocity;

                _driver._frustration = 0.0;
                _driver.CutState(_driver._botEvade);
                return true;
            }


            if (!_driver.Instance.SettingConfig.UseTurnDelay)
            {
                // If trying to move, but can't try evading
                if (currentSpeed > 0.0f)
                {
                    _driver._stuckCount = 0;
                }
                else
                {
                    // Not moving...
                    _driver._stuckCount++;
                    if (_driver._stuckCount > 3)
                    {
                        _driver._frustration = 0.0f;
                        _driver.CutState(_driver._botEvade);
                        return true;
                    }
                }
            }


            // Done seeing if it should evade or continue as normal
            return false;
        }

        private bool IsRecalculationNecessary =>
            _currentRoute.IsFinished ? false :
            // Can't go through storage location unless it's the destination
            _currentRoute.NextStep.PodStorageLocation && _driver.Pod != null && _currentRoute.NextStep != DestinationWaypoint ? true :
            // Can't use the edge to the node if it is dynamically blocked at the moment
            !_currentRoute.First.IsAccessible(_currentRoute.NextStep) ? true :
            // Ignore the waypoint if another bot is already registered to it except for queue WPs (if waiting is enabled for path planning)
            _driver.Config.SimpleWaitingEnabled && _currentRoute.NextStep.AnyBotsOverall && !_currentRoute.NextStep.IsQueueWaypoint;

        private bool IsWaitingNecessary
        {
            get
            {
                if (// Check whether simple waiting is enabled at all
                    !_driver.Config.SimpleWaitingEnabled ? false :
                        // Check simple blocked
                        _currentRoute.IsNextStepBlocked)
                    return true;
                if (// Check whether predictive dodging is enabled at all
                    !_driver.Config.SimpleWaitingD2Enabled ? false :
                        // Check whether we have to wait for an incoming bot at the next step
                        _currentRoute.IsDepth2Approaching)
                    return true;
                if (// Check whether ping pong handling is enabled at all
                    !_driver.Config.PingPongWaitingEnabled ? false :
                    // Check whether there are 3 real waypoints (ignore free roaming)
                    _currentRoute == null || _currentRoute.IsFinished || _driver._currentWaypoint == null || _driver._previousWaypoint == null ? false :
                    // Check whether the first and third match and there was no pod storage in between (wait randomly)
                    _currentRoute.NextStep == _driver._previousWaypoint && !_driver._currentWaypoint.PodStorageLocation && _driver.Instance.Randomizer.NextDouble() < 0.8)
                    return true;
                return false;
            }
        }

        // TODO remove debug
        private static int Recalculate;
        private static int KeepOnGoing;

        public void Act(Bot self, double lastTime, double currentTime)
        {
            // If it's the first time executing this, log the start time of the trip
            if (!_initialized)
            {
                self.StatLastTripStartTime = _driver.CurrentTime;
                _driver.StatTotalStateCounts[Type]++;
                var waypoint = self.CurrentWaypoint ?? self.Instance.WaypointGraph.GetClosestWaypoint(self.Tier, self.X, self.Y);
                self.StatDistanceRequestedOptimal += self.Pod != null ?
                    Distances.CalculateShortestPathPodSafe(waypoint, DestinationWaypoint, self.Instance) :
                    Distances.CalculateShortestPath(waypoint, DestinationWaypoint, self.Instance);
                _initialized = true;
            }

            // See if still cruising
            if (_driver.CurrentTime < _driver.CruiseUntil)
            {
                needToEvade(self);	// See if need to evade before continuing on
                return;
            }

            // If off waypoint map, get back on!
            if (_driver.CurrentWaypoint == null && DestinationWaypoint != null)
                _driver.CurrentWaypoint = _driver.Instance.WaypointGraph.GetClosestWaypoint(_driver.Tier, _driver.X, _driver.Y);

            // If going to a waypoint, then use the waypoint's coordinates
            if (_driver.CurrentWaypoint != null)
            {
                moveToX = _driver.CurrentWaypoint.X;
                moveToY = _driver.CurrentWaypoint.Y;
            }

            // Calculate route if not present yet
            if (_currentRoute == null)
                _currentRoute = _driver.GetNextWaypointTo(_driver.CurrentWaypoint, DestinationWaypoint);
            // Make route public if visualization is present
            if (_driver.Instance.SettingConfig.VisualizationAttached && _currentRoute != null)
                _driver._currentPath = _currentRoute.Route.Cast<IWaypointInfo>().ToList();

            // Signal target to bot-core
            _driver.XTarget = moveToX; _driver.YTarget = moveToY;

            // Find distance to goal
            var goalDistance = _driver.GetDistance(moveToX, moveToY);

            // Make sure tolerance is less than the map's tolerance/3 to make sure that if it sets a pod down,
            // the next one bot will be able to pick it up (and could also be tolerance/3 away)
            // better fudge factor than tolerance / 2 (which is the minimum that will work)
            var tolerance = _driver.Instance.SettingConfig.Tolerance / 3;


            var curSpeed = _driver.GetSpeed(); // Called once for code efficiency (since GetSpeed does a sqrt)

            // If too close to stop, then stop as fast as possible
            var decelTime = curSpeed / _driver.MaxAcceleration;
            var decelDistance = _driver.MaxAcceleration / 2 * decelTime * decelTime;
            if (curSpeed > 0 && decelDistance > goalDistance)
            {
                _driver.SetTargetSpeed(0.0);
                _driver.CruiseUntil = _driver.AccelerateUntil;
                _driver._frustration = (_driver._frustration + 1.0f) / 2;
                curSpeed = _driver.GetSpeed();
            }

            // If close enough to goal and stopped moving, then do next action
            if (goalDistance < tolerance && curSpeed == 0.0)
            {
                // Reset frustration
                _driver._frustration = 0.0;
                // Remove lock on previous waypoint
                _driver.FreePreviousWaypoint();
                // Check whether move task is complete
                if (_driver.CurrentWaypoint == DestinationWaypoint || DestinationWaypoint == null)
                {
                    // Manage connectivity statistics
                    if (DestinationWaypoint != null)
                        _driver.CurrentWaypoint.StatReachedDestination(_driver);
                    // Signal target to bot-core
                    _driver.XTarget = double.NaN; _driver.YTarget = double.NaN;
                    // Remove this task
                    _driver.DequeueState();
                    // Act on next task
                    if (_driver.StateQueue.Any())
                        _driver.StateQueue.First().Act(_driver, lastTime, currentTime);
                    return;
                }
                // See if we have to use an elevator now
                if (_driver.CurrentWaypoint != null && _currentRoute != null && _currentRoute.First == _driver.CurrentWaypoint && // Ensure the bot is on the waypoint system and has a route
                    _currentRoute.HasNextStep && _currentRoute.First.Elevator != null && _currentRoute.NextStep.Elevator == _currentRoute.First.Elevator) // See if the bot is located at an elevator entrance
                {
                    var transportTime = _driver.CurrentWaypoint.Elevator.Transport(_driver, _currentRoute.First, _currentRoute.NextStep);
                    _driver.CurrentWaypoint = _currentRoute.NextStep;
                    _currentRoute.Step();
                    _driver.BlockedUntil = _driver.CurrentTime + transportTime;
                    return;
                }
                // Need to get next location to go to
                if (_currentRoute == null || _currentRoute.IsFinished && _currentRoute.First != DestinationWaypoint || IsRecalculationNecessary)
                {
                    Recalculate++;
                    // No route present or special situation - (re)calculate route for destination
                    var newRoute = _driver.GetNextWaypointTo(_driver.CurrentWaypoint, DestinationWaypoint);
                    // Store it
                    _currentRoute = newRoute;
                    // Make route public if visualization is present
                    if (_driver.Instance.SettingConfig.VisualizationAttached && _currentRoute != null)
                        _driver._currentPath = _currentRoute.Route.Cast<IWaypointInfo>().ToList();
                }
                // Ensure a valid route was calculated
                if (_currentRoute != null && _currentRoute.IsFinished)
                    _currentRoute = null;
                // Check whether waiting or going is preferred
                if (_currentRoute == null || IsWaitingNecessary)
                {
                    // Block the bot for a random amount of time
                    _driver.BlockedUntil = _driver.CurrentTime + _driver.Instance.Randomizer.NextDouble(0.5, 1.5);
                    return;
                }
                // Perform step on route
                _driver.CurrentWaypoint = _currentRoute.NextStep;
                _driver.CurrentWaypoint.Elevator?.InUse = true;
                _currentRoute.Step();
                KeepOnGoing++;

                // Done here
                return;
            }

            // Not at goal, so see if need to evade
            if (needToEvade(self))
                return;

            // If not facing the right way (within tolerance), or heading outside of the map, turn so it is a good direction
            var projectedX = _driver.X + goalDistance * Math.Cos(_driver.GetInfoOrientation());
            var projectedY = _driver.Y + goalDistance * Math.Sin(_driver.GetInfoOrientation());
            if ((projectedX - moveToX) * (projectedX - moveToX) + (projectedY - moveToY) * (projectedY - moveToY) >= tolerance * tolerance
                || projectedX + _driver.Radius > _driver.Tier.Length || projectedX - _driver.Radius < 0.0
                || projectedY + _driver.Radius > _driver.Tier.Width || projectedY - _driver.Radius < 0.0)
            {
                _driver.SetTargetOrientation(Math.Atan2(moveToY - _driver.Y, moveToX - _driver.X));
                if (curSpeed > 0)
                    _driver.SetTargetSpeed(0.0);
                _driver.CruiseUntil = _driver.AccelerateUntil;
                return;
            }

            // Going the right direction - now figure out speed

            // Get new velocity based on frustration
            var newSpeed = _driver.MaxVelocity * Math.Max(1.0 - _driver._frustration, 0.0078125);
            _driver.SetTargetSpeed(newSpeed);
            // Define drive time
            if (_driver.Instance.SettingConfig.UseAcceleration)
            {
                // Get new acceleration based on frustration
                var acceleration = _driver.MaxAcceleration * Math.Max(1.0 - _driver._frustration, 0.0078125);
                // See if have room to accelerate to full speed and still decelerate
                var accelTime = _driver.GetTargetSpeedDifference() / acceleration;
                var accelDistance = curSpeed * accelTime + acceleration / 2 * accelTime * accelTime;
                decelTime = newSpeed / _driver.MaxAcceleration;
                decelDistance = acceleration / 2 * accelTime * accelTime;

                // If enough room to fully accelerate, do so
                if (accelDistance + decelDistance <= goalDistance)
                    _driver.CruiseUntil = _driver.AccelerateUntil;
                else
                {
                    // Don't have time to fully accelerate
                    var cosDir = Math.Cos(_driver.Orientation);
                    var sinDir = Math.Sin(_driver.Orientation);
                    var xAccel = acceleration * cosDir;
                    var yAccel = acceleration * sinDir;
                    if (Math.Abs(xAccel) > Math.Abs(yAccel))
                    {
                        var xGoal = (goalDistance - tolerance) * cosDir;
                        _driver.CruiseUntil = _driver.CurrentTime
                                              + _sqrt2 * (Math.Sqrt(2 * xAccel * xGoal + _driver.XVelocity * _driver.XVelocity) - _sqrt2 * _driver.XVelocity) / (2 * xAccel);
                    }
                    else
                    {
                        var yGoal = (goalDistance - tolerance) * sinDir;
                        _driver.CruiseUntil = _driver.CurrentTime
                                              + _sqrt2 * (Math.Sqrt(2 * yAccel * yGoal + _driver.YVelocity * _driver.YVelocity) - _sqrt2 * _driver.YVelocity) / (2 * yAccel);
                    }
                }
            }
            else
            {
                _driver.CruiseUntil = _driver.CurrentTime + goalDistance / newSpeed;
            }
        }
    }

    #endregion

    #region Evade state

    /// <summary>
    /// The state defining the operation of evasion.
    /// </summary>
    internal class BotEvade : IBotState
    {
        public override string ToString() { return "Evade"; }
        public BotStateType Type => BotStateType.Evade;
        private readonly BotHazard _driver;
        private bool _initialized;

        public BotEvade(BotHazard driver) { _driver = driver; }
        public Waypoint DestinationWaypoint { get; private set; }
        public void Act(Bot self, double lastTime, double currentTime)
        {
            // Initialize
            if (!_initialized) { self.StatTotalStateCounts[Type]++; _initialized = true; }

            if (_driver.CurrentTime < _driver.CruiseUntil)
                return;

            var rand = _driver.Instance.Randomizer;

            // If doing something else (stateQueue isn't empty), are trying to move to a new location,
            // but there's another podbot at that location, then sit and wait most of the time
            if (_driver.NextState is BotMove
                && !_driver.Tier.IsBotMoveValid(self, ((BotMove)_driver.NextState).moveToX, ((BotMove)_driver.NextState).moveToY))
            {
                // Usually sit and wait
                if (rand.NextDouble() < .9)
                    return;
            }

            _driver.SetTargetSpeed(_driver.MaxVelocity);
            // cruiseUntil = curTime + getRadius() / getMaxVelocity() / 2; // TODO remove
            var cruiseTime = _driver.Instance.SettingConfig.UseAcceleration ?
                (Math.Sqrt(2 * _driver.MaxAcceleration * _driver.Radius + _driver.MaxVelocity * _driver.MaxVelocity) - _driver.MaxVelocity) / _driver.MaxAcceleration / 2 :
                _driver.Radius / _driver.MaxVelocity;
            _driver.CruiseUntil = _driver.CurrentTime + cruiseTime;

            var newDirection = _driver.GetBestEvadeDirection(_driver.EvadeDistance);
            if (_driver.Orientation != newDirection)
                _driver.SetTargetOrientation(newDirection);

            // Randomly exit the evasion mode
            if (rand.NextDouble() < .75)
            {
                _driver.DequeueState();
                _driver.CurrentWaypoint = _driver.Instance.WaypointGraph.GetClosestWaypoint(_driver.Tier, _driver.X, _driver.Y);
                if (_driver.StateQueue.Any())
                    _driver.StateQueue.First().Act(_driver, lastTime, currentTime);
            }
        }
    }
    #endregion

    #endregion

    #region Events
    /// <summary>
    /// Called when [bot reached way point].
    /// </summary>
    /// <param name="waypoint">The way point.</param>
    /// <exception cref="System.NotImplementedException"></exception>
    public override void OnReachedWaypoint(Waypoint waypoint)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Called when [bot picked up the pod].
    /// </summary>
    /// <exception cref="System.NotImplementedException"></exception>
    public override void OnPickedUpPod()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Called when [bot set down pod].
    /// </summary>
    /// <exception cref="System.NotImplementedException"></exception>
    public override void OnSetDownPod()
    {
        throw new NotImplementedException();
    }
    #endregion
}