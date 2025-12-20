using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RAWSimO.Core.Configurations;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Interfaces;
using RAWSimO.Core.Randomization;
using RAWSimO.Core.Waypoints;
using System.IO;
using RAWSimO.Core.IO;

namespace RAWSimO.Core.Generator;

internal class LayoutGenerator
{

    #region member variables

    private readonly SettingConfiguration baseConfiguration;
    private readonly IRandomizer rand;
    private readonly Dictionary<Tuple<double, double>, Elevator> elevatorPositions;
    private readonly Dictionary<Elevator, List<Waypoint>> elevatorWaypoints;
    private readonly Dictionary<Waypoint, QueueSemaphore> elevatorSemaphores;
    private readonly double orientationPodDefault = 0;
    private readonly Instance instance;
    private readonly LayoutConfiguration lc;
    private readonly bool _logInfo = true;
    private readonly Action<string> _logAction;

    #endregion

    #region helper methods

    /// <summary>
    /// Helper method projecting boolean direction markers into a direction type.
    /// </summary>
    /// <param name="east">Indicates whether a east direction is desired.</param>
    /// <param name="west">Indicates whether a west direction is desired.</param>
    /// <param name="north">Indicates whether a north direction is desired.</param>
    /// <param name="south">Indicates whether a south direction is desired.</param>
    /// <returns>The direction.</returns>
    internal static directions GetDirectionType(bool east, bool west, bool south, bool north)
    {
        if (east)
        {
            // EAST
            if (west)
            {
                // WEST
                if (north)
                {
                    // NORTH
                    if (south)
                        // SOUTH
                        return directions.EastNorthSouthWest;
                    // NO SOUTH
                    return directions.EastNorthWest;
                }

                // NO NORTH
                if (south)
                    // SOUTH
                    return directions.EastSouthWest;
                // NO SOUTH
                return directions.EastWest;
            }

            // NO WEST
            if (north)
            {
                // NORTH
                if (south)
                    // SOUTH
                    return directions.EastNorthSouth;
                // NO SOUTH
                return directions.EastNorth;
            }

            // NO NORTH
            if (south)
                // SOUTH
                return directions.EastSouth;
            // NO SOUTH
            return directions.East;
        }

        // NO EAST
        if (west)
        {
            // WEST
            if (north)
            {
                // NORTH
                if (south)
                    // SOUTH
                    return directions.NorthSouthWest;
                // NO SOUTH
                return directions.NorthWest;
            }

            // NO NORTH
            if (south)
                // SOUTH
                return directions.SouthWest;
            // NO SOUTH
            return directions.West;
        }

        // NO WEST
        if (north)
        {
            // NORTH
            if (south)
                // SOUTH
                return directions.NorthSouth;
            // NO SOUTH
            return directions.North;
        }

        // NO NORTH
        if (south)
            // SOUTH
            return directions.South;
        // NO SOUTH
        return directions.Invalid;
    }

    /// <summary>
    /// Prints the layout given by the 2D array to the console.
    /// </summary>
    /// <param name="tiles">The layout to print.</param>
    /// <param name="showCols">Shows the column indices instead of the type.</param>
    /// <param name="showRows">Shows the row indices instead of the type.</param>
    internal static void DebugPrintLayout(Tile[,] tiles, bool showRows = false, bool showCols = false)
    {
        var maxRowIndexLength = (tiles.GetLength(0) - 1).ToString().Length;
        var maxColIndexLength = (tiles.GetLength(1) - 1).ToString().Length;
        for (var i = 0; i < tiles.GetLength(0); i++)
        {
            for (var j = 0; j < tiles.GetLength(1); j++)
                Console.Write((
                                  showRows ? i.ToString().PadLeft(maxRowIndexLength) :
                                  showCols ? j.ToString().PadLeft(maxColIndexLength) :
                                  tiles[i, j] != null ? tiles[i, j].directionAsString() : " ") +
                              (j == tiles.GetLength(1) - 1 ? "" : " "));
            Console.WriteLine();
        }
    }

    #endregion

    public LayoutGenerator(
        LayoutConfiguration layoutConfiguration,
        SettingConfiguration baseConfiguration,
        ControlConfiguration controlConfiguration,
        Action<string> logAction = null)
    {
        _logAction = logAction;
        var errorMessage = "";
        if (!layoutConfiguration.isValid(out errorMessage))
        {
            throw new ArgumentException("LayoutConfiguration is not valid. " + errorMessage);
        }

        baseConfiguration.InventoryConfiguration.autogenerate();
        if (!baseConfiguration.InventoryConfiguration.isValid(layoutConfiguration.PodCapacity, out errorMessage))
        {
            throw new ArgumentException("InventoryConfiguration is not valid. " + errorMessage);
        }

        if (!controlConfiguration.IsValid(out errorMessage))
        {
            throw new ArgumentException("ControlConfiguration is not valid. " + errorMessage);
        }

        rand = new RandomizerSimple(layoutConfiguration.Seed);
        this.baseConfiguration = baseConfiguration;
        elevatorPositions = new Dictionary<Tuple<double, double>, Elevator>();
        elevatorWaypoints = new Dictionary<Elevator, List<Waypoint>>();
        elevatorSemaphores = new Dictionary<Waypoint, QueueSemaphore>();
        instance = Instance.CreateInstance(this.baseConfiguration, controlConfiguration);
        instance.Name = layoutConfiguration.NameLayout;
        lc = layoutConfiguration;

    }

    private void Write(string msg)
    {
        _logAction?.Invoke(msg);
        Console.Write(msg);
    }

    public void addTiersToCompound()
    {
        for (var whichTier = 0; whichTier < lc.TierCount; whichTier++)
        {
            double relativePositionX = 0;
            double relativePositionY = 0;
            var relativePositionZ = whichTier * lc.TierHeight;
            instance.CreateTier(instance.RegisterTierID(), lc.lengthTier(), lc.widthTier(), relativePositionX, relativePositionY, relativePositionZ);
        }
    }

    public void connectAllWayPoints(Tile[,] tiles)
    {
        for (var row = 0; row < tiles.GetLength(0); row++)
        {
            for (var column = 0; column < tiles.GetLength(1); column++)
            {
                if (tiles[row, column] != null)
                {
                    var addWest = false;
                    var addEast = false;
                    var addNorth = false;
                    var addSouth = false;
                    switch (tiles[row, column].direction)
                    {
                        case directions.EastNorthSouthWest: addEast = true; addNorth = true; addSouth = true; addWest = true; break;
                        case directions.NorthSouthWest: addNorth = true; addSouth = true; addWest = true; break;
                        case directions.EastNorthSouth: addEast = true; addNorth = true; addSouth = true; break;
                        case directions.EastNorthWest: addEast = true; addNorth = true; addWest = true; break;
                        case directions.EastSouthWest: addEast = true; addSouth = true; addWest = true; break;
                        case directions.NorthSouth: addNorth = true; addSouth = true; break;
                        case directions.NorthWest: addNorth = true; addWest = true; break;
                        case directions.EastNorth: addEast = true; addNorth = true; break;
                        case directions.SouthWest: addSouth = true; addWest = true; break;
                        case directions.EastSouth: addEast = true; addSouth = true; break;
                        case directions.EastWest: addEast = true; addWest = true; break;
                        case directions.East: addEast = true; break;
                        case directions.West: addWest = true; break;
                        case directions.South: addSouth = true; break;
                        case directions.North: addNorth = true; break;
                        case directions.Invalid: throw new ArgumentException("invalid direction encountered");
                        default: break;
                    }
                    var current = tiles[row, column].wp;
                    if (addWest)
                    {
                        var west = tiles[row, column - 1].wp;
                        current.AddPath(west);
                    }
                    if (addEast)
                    {
                        var east = tiles[row, column + 1].wp;
                        current.AddPath(east);
                    }
                    if (addNorth)
                    {
                        var north = tiles[row - 1, column].wp;
                        current.AddPath(north);
                    }
                    if (addSouth)
                    {
                        var south = tiles[row + 1, column].wp;
                        current.AddPath(south);
                    }
                }
            }
        }
    }

    public void connectElevators()
    {
        foreach (var elevator in elevatorWaypoints.Keys)
        {
            // Add all waypoints to the elevator
            elevator.RegisterPoints(0, elevatorWaypoints[elevator]);

            // Set timings for transportation depending on the difference in tier-level
            foreach (var from in elevator.ConnectedPoints)
            foreach (var to in elevator.ConnectedPoints.Where(wp => wp != from))
                elevator.SetTiming(from, to, Math.Abs(from.Tier.ID - to.Tier.ID) * lc.ElevatorTransportationTimePerTier);

            // Connect the waypoints
            foreach (var from in elevator.ConnectedPoints)
            foreach (var to in elevator.ConnectedPoints.Where(wp => wp != from))
                from.AddPath(to);

            // Generate outgoing guards for the queues on each level
            foreach (var from in elevator.ConnectedPoints)
            foreach (var to in elevator.ConnectedPoints.Where(wp => wp != from))
                elevatorSemaphores[from].RegisterGuard(from, to, false, false);
        }
    }

    public void fillTiers()
    {
        var iStationActivationID = 0;
        var oStationActivationID = 0;
        var obtainIStationActivationID = () => { return iStationActivationID++; };
        var obtainOStationActivationID = () => { return oStationActivationID++; };
        foreach (var tier in instance.Compound.Tiers)
        {

            //the stations are generated first, so that generator that makes the storage area and halls knows where the entrances and exits are of the stations.
            //however, the semaphores of the stations can only be created after the halls, because the semaphores need the waypoints that from the halls

            var tiles = new Tile[lc.widthTier(), lc.lengthTier()]; //this keeps track of all the waypoints, their directions and type. This is handy for construction of the layout but also to for example display information in the console during debugging
            var semaphoreGenerator = new SemaphoreGenerator(tiles, lc, instance, elevatorSemaphores);
            var stationGenerator = new StationGenerator(tier, instance, lc, tiles, elevatorPositions, elevatorWaypoints, semaphoreGenerator, obtainOStationActivationID, obtainIStationActivationID);
            stationGenerator.generateStations();
            var storageAreaAndHallsGenerator = new StorageAreaAndHallsGenerator(tier, instance, lc, rand, tiles, stationGenerator);
            storageAreaAndHallsGenerator.createStorageAreaAndHalls();

            if (_logInfo)
            {
                WriteAllDirectionsInfo(tiles);
                WriteAllTypesInfo(tiles);
            }

            generatePods(tier);
            semaphoreGenerator.generateAllSemaphores(); //depends on info from both stationGenerator and storageAreaAndHallsGenerator, therefore construction of semaphores is delayed until here
            connectAllWayPoints(tiles);
            connectElevators();
            generateRobots(tier, tiles);
            instance.Flush();
            locateResourceFiles();
        }
        // Re-order activation sequence (stations on the lowest floor go first, then the ones next to the tier's center and lastly the ones with the lowest ID - this should break all ties)
        var currentActivationID = 1;
        foreach (var station in instance.InputStations.OrderBy(s => s.Tier.ID).ThenBy(s => Metrics.Distances.CalculateEuclid(s.X, s.Y, s.Tier.Length / 2.0, s.Tier.Width / 2.0)).ThenBy(s => s.ID))
            station.ActivationOrderID = currentActivationID++;
        currentActivationID = 1;
        foreach (var station in instance.OutputStations.OrderBy(s => s.Tier.ID).ThenBy(s => Metrics.Distances.CalculateEuclid(s.X, s.Y, s.Tier.Length / 2.0, s.Tier.Width / 2.0)).ThenBy(s => s.ID))
            station.ActivationOrderID = currentActivationID++;
    }

    public Instance GenerateLayout()
    {
        // Init the tiers
        addTiersToCompound();
        // Fill the tiers
        fillTiers();
        // Return the instance
        return instance;
    }

    public void generatePods(Tier tier)
    {
        var podCount = (int)Math.Floor(lc.PodAmount * (lc.nStorageBlocks() * lc.nStorageLocationsPerBlock()));
        var potentialWaypoints = tier.Waypoints.Where(wp => wp.PodStorageLocation).ToList();
        for (var i = 0; i < podCount; i++)
        {
            var waypointIndex = rand.NextInt(potentialWaypoints.Count);
            var chosenWaypoint = potentialWaypoints[waypointIndex];
            var pod = instance.CreatePod(instance.RegisterPodID(), tier, chosenWaypoint, lc.PodRadius, orientationPodDefault, lc.PodCapacity);
            potentialWaypoints.RemoveAt(waypointIndex);
        }
    }

    public void generateRobots(Tier tier, Tile[,] tiles)
    {
        var waypoints = getWaypointsForInitialRobotPositions(tiles);
        var potentialBotLocations = waypoints.SelectMany(w => w).Where(w => w.Pod == null && w.InputStation == null && w.OutputStation == null).ToList();
        for (var i = 0; i < lc.BotCount; i++)
        {
            var randomWaypointIndex = rand.NextInt(potentialBotLocations.Count);
            var botWaypoint = potentialBotLocations[randomWaypointIndex];
            var orientation = 0;
            var bot = instance.CreateBot(instance.RegisterBotID(), tier, botWaypoint.X, botWaypoint.Y, lc.BotRadius, orientation, lc.PodTransferTime, lc.MaxAcceleration, lc.MaxDeceleration, lc.MaxVelocity, lc.TurnSpeed, lc.CollisionPenaltyTime);
            botWaypoint.AddBotApproaching(bot);
            bot.CurrentWaypoint = botWaypoint;
            potentialBotLocations.RemoveAt(randomWaypointIndex);
        }
    }

    public void locateResourceFiles()
    {
        if (baseConfiguration.InventoryConfiguration.ColoredWordConfiguration != null && !string.IsNullOrWhiteSpace(baseConfiguration.InventoryConfiguration.ColoredWordConfiguration.WordFile))
            baseConfiguration.InventoryConfiguration.ColoredWordConfiguration.WordFile = IOHelper.FindResourceFile(baseConfiguration.InventoryConfiguration.ColoredWordConfiguration.WordFile, Directory.GetCurrentDirectory());
        if (baseConfiguration.InventoryConfiguration.FixedInventoryConfiguration != null && !string.IsNullOrWhiteSpace(baseConfiguration.InventoryConfiguration.FixedInventoryConfiguration.OrderFile))
            baseConfiguration.InventoryConfiguration.FixedInventoryConfiguration.OrderFile = IOHelper.FindResourceFile(baseConfiguration.InventoryConfiguration.FixedInventoryConfiguration.OrderFile, Directory.GetCurrentDirectory());
    }

    public List<List<Waypoint>> getWaypointsForInitialRobotPositions(Tile[,] tiles)
    {
        var waypoints = new List<List<Waypoint>>();
        for (var row = 0; row < tiles.GetLength(0); row++)
        {
            waypoints.Add([]);
            for (var column = 0; column < tiles.GetLength(1); column++)
            {
                var tile = tiles[row, column];
                if (tile != null && tile.type.Equals(waypointTypes.Road))
                {
                    var waypoint = tiles[row, column].wp;
                    waypoints.Last().Add(waypoint);
                }
            }
        }
        return waypoints;
    }

    public void WriteAllDirectionsInfo(Tile[,] tiles)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Info about all directions within the generated instance:");
        for (var i = 0; i < tiles.GetLength(0); i++)
        {
            for (var j = 0; j < tiles.GetLength(1); j++)
            {
                if (tiles[i, j] == null)
                {
                    sb.Append("# ");
                }
                else
                {
                    sb.Append(tiles[i, j].directionAsString() + " ");
                }
            }
            sb.AppendLine();
        }
        sb.AppendLine();
        Write(sb.ToString());
    }

    public void WriteAllTypesInfo(Tile[,] tiles)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Info about all types within the generated instance:");
        for (var i = 0; i < tiles.GetLength(0); i++)
        {
            for (var j = 0; j < tiles.GetLength(1); j++)
            {
                if (tiles[i, j] == null)
                {
                    sb.Append("# ");
                }
                else
                {
                    sb.Append(tiles[i, j].typeAsString());
                }
            }
            sb.AppendLine();
        }
        sb.AppendLine();
        Write(sb.ToString());
    }
}

internal enum directions
{
    //this enum indicates to which other waypoints a waypoint is connected. 
    //So for example, East means that a waypoint is only connected to the waypoint directly east of it, 
    //whereas EastNorth would indicate that the waypoint is connected to both the one directly to the east and directly to the north

    //used alphabetical order for ordering directions when there are multiple (i.e. EastNorth instead of NorthEast)

    //single directional:
    East, North, South, West,

    //two directions:
    EastNorth, EastSouth, EastWest,
    NorthSouth, NorthWest,
    SouthWest,

    //three directions:
    EastNorthSouth, EastNorthWest, EastSouthWest, NorthSouthWest,

    //four directions:
    EastNorthSouthWest,

    //invalid value
    Invalid,
}
/// <summary>
/// A subset of the possible directions that is limited to single directions.
/// </summary>
internal enum UniDirections
{
    /// <summary>
    /// An invalid direction.
    /// </summary>
    Invalid,
    /// <summary>
    /// A connection to the east.
    /// </summary>
    East,
    /// <summary>
    /// A connection to the north.
    /// </summary>
    North,
    /// <summary>
    /// A connection to the south.
    /// </summary>
    South,
    /// <summary>
    /// A connection to the west.
    /// </summary>
    West,
}
/// <summary>
/// Distinguishes the different hallways that can be generated.
/// </summary>
public enum HallwayField
{
    /// <summary>
    /// Indicates the eastern hallway field.
    /// </summary>
    East,
    /// <summary>
    /// Indicates the western hallway field.
    /// </summary>
    West,
    /// <summary>
    /// Indicates the southern hallway field.
    /// </summary>
    South,
    /// <summary>
    /// Indicates the northern hallway field.
    /// </summary>
    North,
}

internal enum waypointTypes
{
    Elevator, Road, StorageLocation, PickStation, ReplenishmentStation, Buffer, Invalid
}

/// <summary>
/// Comprises a coordinate.
/// </summary>
internal struct Coordinate
{
    /// <summary>
    /// Creates a new coordinate.
    /// </summary>
    /// <param name="row">The row of the coordinate.</param>
    /// <param name="column">The column of the coordinate.</param>
    public Coordinate(int row, int column) { Row = row; Column = column; }
    /// <summary>
    /// The row.
    /// </summary>
    private readonly int Row;
    /// <summary>
    /// The column.
    /// </summary>
    private readonly int Column;
    /// <summary>
    /// Returns a string representation of the coordinate.
    /// </summary>
    /// <returns>The string representation.</returns>
    public override string ToString() { return $"{Row},{Column}"; }
}

internal class Tile
{
    public Waypoint wp { get; }
    public directions direction { get; }
    public waypointTypes type { get; }

    public Tile(directions d, Waypoint wp, waypointTypes type)
    {
        direction = d;
        this.wp = wp;
        this.type = type;

        if (type.Equals(waypointTypes.StorageLocation) && !d.Equals(directions.EastNorthSouthWest))
        {
            throw new ArgumentException("something went wrong with storage locations");
        }
        if (d.Equals(directions.Invalid))
        {
            throw new ArgumentException("direction invalid");
        }
        if (wp == null)
        {
            throw new ArgumentException("wp is null");
        }
    }

    public String directionAsString()
    {
        return direction switch
        {
            directions.EastNorthSouthWest => "+",
            directions.NorthSouthWest => "<",
            directions.EastNorthSouth => ">",
            directions.EastNorthWest => "^",
            directions.EastSouthWest => "v",
            directions.NorthSouth => "|",
            directions.NorthWest => "d",
            directions.EastNorth => "b",
            directions.SouthWest => "q",
            directions.EastSouth => "p",
            directions.EastWest => "-",
            directions.East => "e",
            directions.West => "w",
            directions.South => "s",
            directions.North => "n",
            directions.Invalid => "INVALID DIRECTION!",
            _ => "SOMETHING WENT WRONG"
        };
    }

    public bool isStation()
    {
        return type.Equals(waypointTypes.PickStation) || type.Equals(waypointTypes.ReplenishmentStation) || type.Equals(waypointTypes.Elevator);
    }

    public String typeAsString()
    {
        return type switch
        {
            waypointTypes.Elevator => "e ",
            waypointTypes.Road => "r ",
            waypointTypes.StorageLocation => "s ",
            waypointTypes.Buffer => "b ",
            waypointTypes.PickStation => "o ",
            waypointTypes.ReplenishmentStation => "i ",
            waypointTypes.Invalid => "INVALID DIRECTION!",
            _ => "SOMETHING WENT WRONG"
        };
    }
}