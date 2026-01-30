using RAWSimO.Core.Configurations;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Waypoints;
using System;
using System.Collections.Generic;

namespace RAWSimO.Core.Generator;

internal class StationGenerator
{
    private readonly Tier tier;
    private readonly Instance instance;
    private readonly LayoutConfiguration lc;
    private readonly Tile[,] tiles;
    private readonly Dictionary<Tuple<double, double>, Elevator> elevatorPositions;
    private readonly Dictionary<Elevator, List<Waypoint>> elevatorWaypoints;
    private readonly SemaphoreGenerator semaphoreGenerator;
    private readonly Func<int> obtainNextOStationActivationID;
    private readonly Func<int> obtainNextIStationActivationID;

    public HashSet<Coordinate> coordinatesStationEntrances { get; } = [];

    public StationGenerator(Tier tier, Instance instance, LayoutConfiguration layoutConfiguration, Tile[,] tiles,
        Dictionary<Tuple<double, double>, Elevator> elevatorPositions,
        Dictionary<Elevator, List<Waypoint>> elevatorWaypoints, SemaphoreGenerator semaphoreGenerator,
        Func<int> obtainNextOStationActivationID, Func<int> obtainNextIStationActivationID)
    {
        this.tier = tier;
        this.instance = instance;
        lc = layoutConfiguration;
        this.tiles = tiles;
        this.elevatorPositions = elevatorPositions;
        this.elevatorWaypoints = elevatorWaypoints;
        this.semaphoreGenerator = semaphoreGenerator;
        this.obtainNextOStationActivationID = obtainNextOStationActivationID;
        this.obtainNextIStationActivationID = obtainNextIStationActivationID;
    }

    public void addToBufferPath(bool stationWasBuilt, Tile tile, List<Waypoint> bufferPath)
    {
        //the buffer path is a from the station way point to the entrance of the buffer
        //this has two consequences:
        //1) waypoints between the station waypoint and the exit of the buffer are not included
        //2) waypoints between the station waypoint and the entrance of the buffer should be included in the reverse order in which they were created
        //note that the station itself also has to be added
        if (!stationWasBuilt || tile.isStation())
        {
            bufferPath.Insert(0, tile.wp);
        }
    }

    public void buildElevator(int row, int column, Directions d, List<Waypoint> bufferPaths)
    {
        //have to determine x and y here to see whether an elevator for that position already exists
        var y = row + 0.5;
        var x = column + 0.5;

        var elevatorPosition = new Tuple<double, double>(x, y);
        var elevatorAtThisPositionAlreadyExists = elevatorPositions.ContainsKey(elevatorPosition);
        var elevator = elevatorAtThisPositionAlreadyExists
            ? elevatorPositions[elevatorPosition]
            : instance.CreateElevator(instance.RegisterElevatorID());

        createTile_Elevator(row, column, d, elevator);
        var elevatorWaypoint = tiles[row, column].wp;

        if (elevatorWaypoint.X != x || elevatorWaypoint.Y != y)
        {
            throw new ArgumentException(
                "something went wrong here while building the elevator, elevatorWaypoint.X != x || elevatorWaypoint.Y != y");
        }

        if (!elevatorAtThisPositionAlreadyExists)
        {
            elevatorPositions.Add(elevatorPosition, elevator);
            elevatorWaypoints[elevator] = [];
        }

        elevatorWaypoints[elevator].Add(elevatorWaypoint);
        elevator.Queues[elevatorWaypoint] = bufferPaths;
    }

    public void buildPickStation(int row, int column, Directions d, List<Waypoint> bufferPaths, int activationOrderID)
    {
        var oStation = instance.CreateOutputStation(
            instance.RegisterOutputStationID(), tier, column + 0.5, row + 0.5, lc.StationRadius, lc.OStationCapacity,
            lc.ItemTransferTime, lc.ItemPickTime, activationOrderID);
        createTile_PickStation(row, column, d, oStation);
        var wp = tiles[row, column].wp;
        oStation.Queues[wp] = bufferPaths;
    }

    public void buildReplenishmentStation(int row, int column, Directions d, List<Waypoint> bufferPaths,
        int activationOrderID)
    {
        var iStation = instance.CreateInputStation(
            instance.RegisterInputStationID(), tier, column + 0.5, row + 0.5, lc.StationRadius, lc.IStationCapacity,
            lc.ItemBundleTransferTime, activationOrderID);
        createTile_ReplenishmentStation(row, column, d, iStation);
        var wp = tiles[row, column].wp;
        iStation.Queues[wp] = bufferPaths;
    }

    public void buildStation(waypointTypes typeOfStation, int row, int column, Directions d, List<Waypoint> bufferPaths,
        int activationOrderID)
    {
        switch (typeOfStation)
        {
            case waypointTypes.PickStation:
                buildPickStation(row, column, d, bufferPaths, activationOrderID);
                break;
            case waypointTypes.ReplenishmentStation:
                buildReplenishmentStation(row, column, d, bufferPaths, activationOrderID);
                break;
            case waypointTypes.Elevator:
                buildElevator(row, column, d, bufferPaths);
                break;
            default:
                throw new ArgumentException("should built station, but typeOfStations is: " + typeOfStation);
        }
    }

    public void buildStationAndBuffer(int exitRow, int exitColumn, int entryRow, int entryColumn, int stationRow,
        int stationColumn, waypointTypes typeOfStation, HashSet<Coordinate> coordinatesStationEntrances,
        int entranceCounter, int activationOrderID)
    {
        checkValidityInputArgumentsBuildStationAndBuffer(exitRow, exitColumn, entryRow, entryColumn, stationRow,
            stationColumn, typeOfStation);

        var lengthSegment = Math.Max(Math.Abs(exitRow - stationRow), Math.Abs(exitColumn - stationColumn)) + 1;
        var nSegments = Math.Max(Math.Abs(exitRow - entryRow), Math.Abs(exitColumn - entryColumn)) + 1;

        var rowDiffToNextTile = exitRow > stationRow ? -1 : exitRow < stationRow ? 1 : 0;
        var columnDiffToNextTile = exitColumn > stationColumn ? -1 : exitColumn < stationColumn ? 1 : 0;
        ;
        var rowDiffToNextSegment = entryRow > exitRow ? -1 : entryRow < exitRow ? 1 : 0;
        ;
        var columnDiffToNextSegment = entryColumn > exitColumn ? -1 : entryColumn < exitColumn ? 1 : 0;

        if (lengthSegment <= 0 || lengthSegment != lc.WidthBuffer)
        {
            throw new ArgumentException(
                "something went wrong while constructing the station, lengthSegment <= 0 || lengthSegment != lc.WidthBuffer, lengthSegment: " +
                lengthSegment);
        }

        if (nSegments <= 0 || nSegments % 2 != 0)
        {
            throw new ArgumentException(
                "something went wrong while constructing the station, nSegments <= 0 || nSegments % 2 != 0, nSegments: " +
                nSegments);
        }

        var bufferPath = new List<Waypoint>();
        var row = entryRow;
        var column = entryColumn;
        var stationWasBuilt = false;
        for (var segment = 0; segment < nSegments; segment++)
        {
            if (segment % 2 == 0)
            {
                coordinatesStationEntrances.Add(new Coordinate(row - rowDiffToNextTile, column - columnDiffToNextTile));
            }

            for (var place = 0; place < lengthSegment; place++)
            {
                var d = determineDirection(place, segment, lengthSegment, nSegments, rowDiffToNextTile,
                    columnDiffToNextTile, rowDiffToNextSegment, columnDiffToNextSegment);
                if (row == stationRow && column == stationColumn)
                {
                    buildStation(typeOfStation, row, column, d, bufferPath, activationOrderID);
                    stationWasBuilt = true;
                }
                else
                {
                    createTile_Buffer(row, column, d);
                }

                var location = tiles[row, column].wp;
                semaphoreGenerator.update(row, column, segment, place, lengthSegment, nSegments, tiles[row, column],
                    rowDiffToNextTile, columnDiffToNextTile, rowDiffToNextSegment, columnDiffToNextSegment);
                addToBufferPath(stationWasBuilt, tiles[row, column], bufferPath);
                row += place == lengthSegment - 1 ? 0 : rowDiffToNextTile;
                column += place == lengthSegment - 1 ? 0 : columnDiffToNextTile;
            }

            rowDiffToNextTile = rowDiffToNextTile != 0 ? -rowDiffToNextTile : rowDiffToNextTile;
            columnDiffToNextTile = columnDiffToNextTile != 0 ? -columnDiffToNextTile : columnDiffToNextTile;
            row += rowDiffToNextSegment;
            column += columnDiffToNextSegment;
        }

        if (!stationWasBuilt)
        {
            throw new ArgumentException("!stationWasBuilt");
        }
    }

    public Directions buildStationAndBuffer_normalDirection(int rowDiff, int columnDiff)
    {
        return rowDiff == -1 ? Directions.North :
            rowDiff == 1 ? Directions.South :
            columnDiff == -1 ? Directions.West :
            columnDiff == 1 ? Directions.East : Directions.Invalid;
    }

    public Directions buildStationAndBuffer_shortcutDirection(int rowDiffToNextTile, int columnDiffToNextTile,
        int rowDiffToNextSegment, int columnDiffToNextSegment)
    {
        if ((rowDiffToNextTile == -1 && columnDiffToNextSegment == 1) ||
            (rowDiffToNextSegment == -1 && columnDiffToNextTile == 1))
        {
            return Directions.EastNorth;
        }

        if ((rowDiffToNextTile == 1 && columnDiffToNextSegment == 1) ||
            (rowDiffToNextSegment == 1 && columnDiffToNextTile == 1))
        {
            return Directions.EastSouth;
        }

        if ((rowDiffToNextTile == -1 && columnDiffToNextSegment == -1) ||
            (rowDiffToNextSegment == -1 && columnDiffToNextTile == -1))
        {
            return Directions.NorthWest;
        }

        if ((rowDiffToNextTile == 1 && columnDiffToNextSegment == -1) ||
            (rowDiffToNextSegment == 1 && columnDiffToNextTile == -1))
        {
            return Directions.SouthWest;
        }

        return Directions.Invalid;
    }

    public int calculateNrPossibleStations(int nAisles)
    {
        return lc.AislesTwoDirectional && lc.DistanceEntryExitStation < lc.minDistanceExits() ? nAisles : nAisles / 2;
    }

    public void checkValidityInputArgumentsBuildStationAndBuffer(int exitRow, int exitColumn, int entryRow,
        int entryColumn, int stationRow, int stationColumn, waypointTypes typeOfStation)
    {
        //check validity input arguments
        if (exitRow < 0 || stationRow < 0 || entryRow < 0 || exitColumn < 0 || stationColumn < 0 || entryColumn < 0)
        {
            throw new ArgumentException(
                "something went wrong while constructing the station, one or more are negative, exitRow: " + exitRow +
                ", stationRow: " + stationRow + ", entryRow: " + entryRow + ", exitColumn: " + exitColumn +
                ", stationColumn: " + stationColumn + ", entryColumn: " + entryColumn);
        }

        if (exitRow != stationRow && exitColumn != stationColumn)
        {
            throw new ArgumentException(
                "something went wrong while constructing the station, exitRow != stationRow && exitColumn != stationColumn, exitRow: " +
                exitRow + ", stationRow: " + stationRow + ", exitColumn: " + exitColumn + ", stationColumn: " +
                stationColumn);
        }

        if (exitRow != entryRow && exitColumn != entryColumn)
        {
            throw new ArgumentException(
                "something went wrong while constructing the station, exitRow != entryRow && exitColumn != entryColumn, exitRow: " +
                exitRow + ", entryRow: " + entryRow + ", exitColumn: " + exitColumn + ", entryColumn: " + entryColumn);
        }

        if (exitRow == stationRow && exitColumn == stationColumn)
        {
            throw new ArgumentException(
                "something went wrong while constructing the station, exitRow == stationRow && exitColumn == stationColumn, exitRow: " +
                exitRow + ", exitColumn: " + exitColumn);
        }

        if (exitRow == entryRow && exitColumn == entryColumn)
        {
            throw new ArgumentException(
                "something went wrong while constructing the station, exitRow == entryRow && exitColumn == entryColumn, exitRow: " +
                exitRow + ", exitColumn: " + exitColumn);
        }

        if (stationColumn == entryRow && stationColumn == entryColumn)
        {
            throw new ArgumentException(
                "something went wrong while constructing the station, stationRow == entryRow && stationColumn == entryColumn, stationRow: " +
                stationRow + ", stationColumn: " + stationColumn);
        }

        if (!typeOfStation.Equals(waypointTypes.PickStation) &&
            !typeOfStation.Equals(waypointTypes.ReplenishmentStation) && !typeOfStation.Equals(waypointTypes.Elevator))
        {
            throw new ArgumentException(
                "!typeOfStation.Equals(waypointTypes.PickStation) && !typeOfStation.Equals(waypointTypes.ReplenishmentStation) && !typeOfStation.Equals(waypointTypes.Elevator)");
        }
    }

    public void createTile_Buffer(int row, int column, Directions d)
    {
        var wp = instance.CreateWaypoint(instance.RegisterWaypointID(), tier, column + 0.5, row + 0.5, false, true);
        if (tiles[row, column] != null)
        {
            throw new ArgumentException(
                "trying to overwrite an existing waypoint!! At createTile_Buffer: tiles[row, column] != null");
        }

        tiles[row, column] = new Tile(d, wp, waypointTypes.Buffer);
    }

    public void createTile_Elevator(int row, int column, Directions d, Elevator elevator)
    {
        var wp = instance.CreateWaypoint(instance.RegisterWaypointID(), tier, elevator, column + 0.5, row + 0.5, true);
        if (tiles[row, column] != null)
        {
            throw new ArgumentException(
                "trying to overwrite an existing waypoint!! At createTile_Elevator: tiles[row, column] != null");
        }

        tiles[row, column] = new Tile(d, wp, waypointTypes.Elevator);
    }

    public void createTile_PickStation(int row, int column, Directions d, OutputStation oStation)
    {
        var wp = instance.CreateWaypoint(instance.RegisterWaypointID(), tier, oStation, true);
        if (tiles[row, column] != null)
        {
            throw new ArgumentException(
                "trying to overwrite an existing waypoint!! At createTile_PickStation: tiles[row, column] != null");
        }

        tiles[row, column] = new Tile(d, wp, waypointTypes.PickStation);
    }

    public void createTile_ReplenishmentStation(int row, int column, Directions d, InputStation iStation)
    {
        var wp = instance.CreateWaypoint(instance.RegisterWaypointID(), tier, iStation, true);
        if (tiles[row, column] != null)
        {
            throw new ArgumentException(
                "trying to overwrite an existing waypoint!! At createTile_ReplenishmentStation: tiles[row, column] != null");
        }

        tiles[row, column] = new Tile(d, wp, waypointTypes.ReplenishmentStation);
    }

    public Directions determineDirection(int place, int segment, int lengthSegment, int nSegments,
        int rowDiffToNextTile, int columnDiffToNextTile, int rowDiffToNextSegment, int columnDiffToNextSegment)
    {
        var shortcutDirection = buildStationAndBuffer_shortcutDirection(rowDiffToNextTile, columnDiffToNextTile,
            rowDiffToNextSegment, columnDiffToNextSegment);
        var toNewSegment = buildStationAndBuffer_normalDirection(rowDiffToNextSegment, columnDiffToNextSegment);
        var toNextPlace = buildStationAndBuffer_normalDirection(rowDiffToNextTile, columnDiffToNextTile);
        var conditionForShortcut = segment % 2 == 1 && place == 0 && segment != nSegments - 1;
        var conditionToNexSegment = place == lengthSegment - 1 && segment != nSegments - 1;
        return conditionForShortcut ? shortcutDirection : conditionToNexSegment ? toNewSegment : toNextPlace;
    }

    public int distanceFirstExitOfStationFromSideOfWarehouse(bool otherHallPresentThatIncreasesDistance, int blockSize,
        bool eastOrNorthHall)
    {
        //for the north and south hall this is the distance to the west side of the warehouse
        //for the west and east hall this is the distance to the north side of the warehouse

        var distanceDueToOtherSideHall = otherHallPresentThatIncreasesDistance ? lc.WidthBuffer + lc.WidthHall : 0;
        var distanceDueToRingway = 1;
        var distanceBetweenFirstAndSecondAisle = blockSize + lc.widthAisles();
        var includeDistanceBetweenFirstAndSecondAisle = lc.AislesTwoDirectional
            ? false
            : (eastOrNorthHall && !lc.CounterClockwiseRingwayDirection) ||
              (!eastOrNorthHall && lc.CounterClockwiseRingwayDirection);
        var distanceDueToAisleWidth = !lc.SingleLane && !lc.AislesTwoDirectional &&
                                      ((eastOrNorthHall && !lc.CounterClockwiseRingwayDirection) ||
                                       (!eastOrNorthHall && lc.CounterClockwiseRingwayDirection))
            ? 1
            : 0;
        return distanceDueToOtherSideHall + distanceDueToRingway + blockSize +
               (includeDistanceBetweenFirstAndSecondAisle ? distanceBetweenFirstAndSecondAisle : 0) +
               distanceDueToAisleWidth;
    }

    public void generateStations()
    {
        if (lc.hasStationsEast())
        {
            generateStationsEast();
        }

        if (lc.hasStationsWest())
        {
            generateStationsWest();
        }

        if (lc.hasStationsNorth())
        {
            generateStationsNorth();
        }

        if (lc.hasStationsSouth())
        {
            generateStationsSouth();
        }
    }

    public void generateStationsNorth()
    {
        var rowDiffEntry = 0;
        var columnDiffEntry = lc.CounterClockwiseRingwayDirection
            ? lc.DistanceEntryExitStation
            : -lc.DistanceEntryExitStation;
        var rowDiffStation = -(lc.WidthBuffer - 1);
        var columnDiffStation = 0;
        var rowFirstExit = lc.WidthBuffer - 1;
        var columnFirstExit =
            distanceFirstExitOfStationFromSideOfWarehouse(lc.hasStationsWest(), lc.HorizontalLengthBlock, true);
        var rowDiffExits = 0;
        var columnDiffExits = lc.distanceBetweenPossibleExitLocationsAtNorthOrSouthHall();
        var nPossibleStations = calculateNrPossibleStations(lc.NrVerticalAisles);
        var possibleLocationsForExits = possibleLocationsForExitsStations(rowFirstExit, columnFirstExit, rowDiffExits,
            columnDiffExits, nPossibleStations);
        generateStationsOnOneSide(possibleLocationsForExits, lc.NPickStationNorth, lc.NReplenishmentStationNorth,
            lc.NElevatorsNorth, rowDiffEntry, columnDiffEntry, rowDiffStation, columnDiffStation,
            coordinatesStationEntrances);
    }

    public void generateStationsSouth()
    {
        var rowDiffEntry = 0;
        var columnDiffEntry = lc.CounterClockwiseRingwayDirection
            ? -lc.DistanceEntryExitStation
            : lc.DistanceEntryExitStation;
        var rowDiffStation = lc.WidthBuffer - 1;
        var columnDiffStation = 0;
        var rowFirstExit = lc.widthTier() - lc.WidthBuffer;
        var columnFirstExit =
            distanceFirstExitOfStationFromSideOfWarehouse(lc.hasStationsWest(), lc.HorizontalLengthBlock, false);
        var rowDiffExits = 0;
        var columnDiffExits = lc.distanceBetweenPossibleExitLocationsAtNorthOrSouthHall();
        var nPossibleStations = calculateNrPossibleStations(lc.NrVerticalAisles);
        var possibleLocationsForExits = possibleLocationsForExitsStations(rowFirstExit, columnFirstExit, rowDiffExits,
            columnDiffExits, nPossibleStations);
        generateStationsOnOneSide(possibleLocationsForExits, lc.NPickStationSouth, lc.NReplenishmentStationSouth,
            lc.NElevatorsSouth, rowDiffEntry, columnDiffEntry, rowDiffStation, columnDiffStation,
            coordinatesStationEntrances);
    }

    public void generateStationsWest()
    {
        var rowDiffEntry = lc.CounterClockwiseRingwayDirection
            ? -lc.DistanceEntryExitStation
            : lc.DistanceEntryExitStation;
        var columnDiffEntry = 0;
        var rowDiffStation = 0;
        var columnDiffStation = -(lc.WidthBuffer - 1);
        var rowFirstExit =
            distanceFirstExitOfStationFromSideOfWarehouse(lc.hasStationsNorth(), lc.VerticalLengthBlock, false);
        var columnFirstExit = lc.WidthBuffer - 1;
        var rowDiffExits = lc.distanceBetweenPossibleExitLocationsAtWestOrEastHall();
        var columnDiffExits = 0;
        var nPossibleStations = calculateNrPossibleStations(lc.NrHorizontalAisles);
        var possibleLocationsForExits = possibleLocationsForExitsStations(rowFirstExit, columnFirstExit, rowDiffExits,
            columnDiffExits, nPossibleStations);
        generateStationsOnOneSide(possibleLocationsForExits, lc.NPickStationWest, lc.NReplenishmentStationWest,
            lc.NElevatorsWest, rowDiffEntry, columnDiffEntry, rowDiffStation, columnDiffStation,
            coordinatesStationEntrances);
    }

    public void generateStationsEast()
    {
        var rowDiffEntry = lc.CounterClockwiseRingwayDirection
            ? lc.DistanceEntryExitStation
            : -lc.DistanceEntryExitStation;
        var columnDiffEntry = 0;
        var rowDiffStation = 0;
        var columnDiffStation = lc.WidthBuffer - 1;
        var rowFirstExit =
            distanceFirstExitOfStationFromSideOfWarehouse(lc.hasStationsNorth(), lc.VerticalLengthBlock, true);
        var columnFirstExit = lc.lengthTier() - lc.WidthBuffer;
        var rowDiffExits = lc.distanceBetweenPossibleExitLocationsAtWestOrEastHall();
        var columnDiffExits = 0;
        var nPossibleStations = calculateNrPossibleStations(lc.NrHorizontalAisles);
        var possibleLocationsForExits = possibleLocationsForExitsStations(rowFirstExit, columnFirstExit, rowDiffExits,
            columnDiffExits, nPossibleStations);
        generateStationsOnOneSide(possibleLocationsForExits, lc.NPickStationEast, lc.NReplenishmentStationEast,
            lc.NElevatorsEast, rowDiffEntry, columnDiffEntry, rowDiffStation, columnDiffStation,
            coordinatesStationEntrances);
    }

    public void generateStationsOnOneSide(int[,] possibleLocationsForExits, int nPickStation,
        int nReplenishmentStations, int nElevators, int rowDiffEntry, int columnDiffEntry, int rowDiffStation,
        int columnDiffStation, HashSet<Coordinate> coordinatesStationEntrances)
    {
        var exits = selectLocations(possibleLocationsForExits, nPickStation + nReplenishmentStations + nElevators);
        var entranceCounter = 0;
        for (var whichStation = 0; whichStation < exits.GetLength(0); whichStation++)
        {
            var exitRow = exits[whichStation, 0];
            var exitColumn = exits[whichStation, 1];
            var entryRow = exits[whichStation, 0] + rowDiffEntry;
            var entryColumn = exits[whichStation, 1] + columnDiffEntry;
            var stationRow = exits[whichStation, 0] + rowDiffStation;
            var stationColumn = exits[whichStation, 1] + columnDiffStation;
            var typeOfStation = whichStation < nPickStation ? waypointTypes.PickStation :
                whichStation < nPickStation + nReplenishmentStations ? waypointTypes.ReplenishmentStation :
                waypointTypes.Elevator;
            var activationID = 0;
            switch (typeOfStation)
            {
                case waypointTypes.PickStation: activationID = obtainNextOStationActivationID(); break;
                case waypointTypes.ReplenishmentStation: activationID = obtainNextIStationActivationID(); break;
                default: break;
            }

            buildStationAndBuffer(exitRow, exitColumn, entryRow, entryColumn, stationRow, stationColumn, typeOfStation,
                coordinatesStationEntrances, entranceCounter, activationID);
            entranceCounter += lc.nEntrancesPerStation();
        }
    }

    public int[,] possibleLocationsForExitsStations(int rowFirstExit, int columnFirstExit, int rowDiffExits,
        int columnDiffExits, int nPossibleStations)
    {
        var possibleLocationsForExits = new int[nPossibleStations, 2];
        possibleLocationsForExits[0, 0] = rowFirstExit;
        possibleLocationsForExits[0, 1] = columnFirstExit;
        for (var whichExit = 1; whichExit < nPossibleStations; whichExit++)
        {
            possibleLocationsForExits[whichExit, 0] = possibleLocationsForExits[whichExit - 1, 0] + rowDiffExits;
            possibleLocationsForExits[whichExit, 1] = possibleLocationsForExits[whichExit - 1, 1] + columnDiffExits;
        }

        return possibleLocationsForExits;
    }

    public int[,] selectLocations(int[,] possibleLocations, int nStationsInTotal)
    {
        if (nStationsInTotal > possibleLocations.GetLength(0))
        {
            throw new ArgumentException("nStationsInTotal > possibleLocations.GetLength(0)");
        }

        if (possibleLocations.GetLength(1) != 2)
        {
            throw new ArgumentException("possibleLocations.GetLength(1) != 2");
        }

        var locations = new int[nStationsInTotal, 2];

        var start = (possibleLocations.GetLength(0) - nStationsInTotal) / 2;
        for (var i = 0; i < nStationsInTotal; i++)
        {
            locations[i, 0] = possibleLocations[start + i, 0];
            locations[i, 1] = possibleLocations[start + i, 1];
        }

        return locations;
    }
}