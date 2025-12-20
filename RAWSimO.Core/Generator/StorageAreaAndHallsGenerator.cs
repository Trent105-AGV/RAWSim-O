using RAWSimO.Core.Configurations;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Interfaces;
using RAWSimO.Core.Waypoints;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RAWSimO.Core.Generator;

internal class StorageAreaAndHallsGenerator
{
    private readonly Tier tier;
    private readonly Instance instance;
    private readonly LayoutConfiguration lc;
    private readonly Tile[,] tiles;
    private IRandomizer rand;
    private readonly StationGenerator stationGenerator;

    public StorageAreaAndHallsGenerator(Tier tier, Instance instance, LayoutConfiguration layoutConfiguration, IRandomizer rand, Tile[,] tiles, StationGenerator stationGenerator)
    {
        this.tier = tier;
        this.instance = instance;
        lc = layoutConfiguration;
        this.rand = rand;
        this.tiles = tiles;
        this.stationGenerator = stationGenerator;
    }

    public void createAisleHorizontally(int row, int startColumn, directions directionHorizontally, directions directionCrossPoint)
    {
        var endColumn = lc.lengthStorageArea() - 2 * lc.WidthRingway;
        var lengthBlock = lc.HorizontalLengthBlock + lc.widthAisles();
        for (var column = startColumn; column < startColumn + endColumn; column++)
        {
            directions d;
            if ((column - startColumn) % lengthBlock == lengthBlock - 1 || (!lc.SingleLane && (column - startColumn) % lengthBlock == lengthBlock - 2))
            {
                d = directionCrossPoint;
                directionCrossPoint = lc.AislesTwoDirectional ? directions.EastNorthSouthWest : directionCrossPoint.Equals(directions.NorthWest) ? directions.SouthWest : directionCrossPoint.Equals(directions.SouthWest) ? directions.NorthWest : directionCrossPoint.Equals(directions.EastNorth) ? directions.EastSouth : directionCrossPoint.Equals(directions.EastSouth) ? directions.EastNorth : directions.Invalid;
            }
            else
            {
                d = directionHorizontally;
            }
            createTile_Road(row, column, d);
        }
    }

    public void createDirectionsForSpaceWestOfStorageArea(int row, int column, int length, int width)
    {

        if (width % 2 != 0)
        {
            throw new ArgumentException("createSpaceWestOfStorageArea, width %2 != 0, width: " + width);
        }

        //the main part of this space
        var d = lc.CounterClockwiseRingwayDirection ? directions.East : directions.West;
        for (var i = 0; i < width; i++)
        {
            for (var j = 2; j < length; j++)
            {
                createTile_Road(row + i, column + j, d);
            }
            d = d.Equals(directions.East) ? directions.West : directions.East;
        }

        //side at the workstations
        for (var i = 1; i < width - 1; i += 2)
        {
            d = isEntrance(row + i, column, stationGenerator.coordinatesStationEntrances) ? lc.CounterClockwiseRingwayDirection ? directions.NorthWest : directions.EastSouthWest : lc.CounterClockwiseRingwayDirection ? directions.North : directions.EastSouth;
            createTile_Road(row + i, column, d);
            d = lc.CounterClockwiseRingwayDirection ? directions.SouthWest : directions.EastNorth;
            createTile_Road(row + i, column + 1, d);
            d = isEntrance(row + i + 1, column, stationGenerator.coordinatesStationEntrances) ? lc.CounterClockwiseRingwayDirection ? directions.EastNorthWest : directions.SouthWest : lc.CounterClockwiseRingwayDirection ? directions.EastNorth : directions.South;
            createTile_Road(row + i + 1, column, d);
            d = lc.CounterClockwiseRingwayDirection ? directions.EastSouth : directions.NorthWest;
            createTile_Road(row + i + 1, column + 1, d);
        }

        //the corners
        d = isEntrance(row, column, stationGenerator.coordinatesStationEntrances) ? lc.CounterClockwiseRingwayDirection ? directions.EastWest : directions.SouthWest : lc.CounterClockwiseRingwayDirection ? directions.East : directions.South;
        createTile_Road(row, column, d);
        d = lc.CounterClockwiseRingwayDirection ? directions.EastSouth : directions.West;
        createTile_Road(row, column + 1, d);
        d = isEntrance(row + width - 1, column, stationGenerator.coordinatesStationEntrances) ? lc.CounterClockwiseRingwayDirection ? directions.NorthWest : directions.EastWest : lc.CounterClockwiseRingwayDirection ? directions.North : directions.East;
        createTile_Road(row + width - 1, column, d);
        d = lc.CounterClockwiseRingwayDirection ? directions.West : directions.EastNorth;
        createTile_Road(row + width - 1, column + 1, d);
    }

    public void createDirectionsForSpaceEastOfStorageArea(int row, int column, int length, int width)
    {
        if (width % 2 != 0)
        {
            throw new ArgumentException("createSpaceEastOfStorageArea, width %2 != 0, width: " + width);
        }

        //the main part of this space
        var d = lc.CounterClockwiseRingwayDirection ? directions.East : directions.West;
        for (var i = 0; i < width; i++)
        {
            for (var j = 0; j < length - 2; j++)
            {
                createTile_Road(row + i, column + j, d);
            }
            d = d.Equals(directions.East) ? directions.West : directions.East;
        }

        //side at the workstations
        for (var i = 1; i < width - 1; i += 2)
        {
            d = lc.CounterClockwiseRingwayDirection ? directions.NorthWest : directions.EastSouth;
            createTile_Road(row + i, column + length - 2, d);
            d = isEntrance(row + i, column + length - 1, stationGenerator.coordinatesStationEntrances) ? lc.CounterClockwiseRingwayDirection ? directions.EastSouthWest : directions.EastNorth : lc.CounterClockwiseRingwayDirection ? directions.SouthWest : directions.North;
            createTile_Road(row + i, column + length - 1, d);
            d = lc.CounterClockwiseRingwayDirection ? directions.EastNorth : directions.SouthWest;
            createTile_Road(row + i + 1, column + length - 2, d);
            d = isEntrance(row + i + 1, column + length - 1, stationGenerator.coordinatesStationEntrances) ? lc.CounterClockwiseRingwayDirection ? directions.EastSouth : directions.EastNorthWest : lc.CounterClockwiseRingwayDirection ? directions.South : directions.NorthWest;
            createTile_Road(row + i + 1, column + length - 1, d);
        }

        //the corners
        d = lc.CounterClockwiseRingwayDirection ? directions.East : directions.SouthWest;
        createTile_Road(row, column + length - 2, d);
        d = isEntrance(row, column + length - 1, stationGenerator.coordinatesStationEntrances) ? lc.CounterClockwiseRingwayDirection ? directions.EastSouth : directions.EastWest : lc.CounterClockwiseRingwayDirection ? directions.South : directions.West;
        createTile_Road(row, column + length - 1, d);
        d = lc.CounterClockwiseRingwayDirection ? directions.NorthWest : directions.East;
        createTile_Road(row + width - 1, column + length - 2, d);
        d = isEntrance(row + width - 1, column + length - 1, stationGenerator.coordinatesStationEntrances) ? lc.CounterClockwiseRingwayDirection ? directions.EastWest : directions.EastNorth : lc.CounterClockwiseRingwayDirection ? directions.West : directions.North;
        createTile_Road(row + width - 1, column + length - 1, d);
    }

    public void createDirectionsForSpaceNorthOfStorageArea(int row, int column, int length, int width)
    {
        if (length % 2 != 0)
        {
            throw new ArgumentException("createSpaceNorthOfStorageArea, length %2 != 0, length: " + length);
        }

        //the main part of this space
        var d = lc.CounterClockwiseRingwayDirection ? directions.North : directions.South;
        for (var j = 0; j < length; j++)
        {
            for (var i = 2; i < width; i++)
            {
                createTile_Road(row + i, column + j, d);
            }
            d = d.Equals(directions.North) ? directions.South : directions.North;
        }

        //side at the workstations
        for (var j = 1; j < length - 1; j += 2)
        {
            d = isEntrance(row, column + j, stationGenerator.coordinatesStationEntrances) ? lc.CounterClockwiseRingwayDirection ? directions.EastNorthSouth : directions.NorthWest : lc.CounterClockwiseRingwayDirection ? directions.EastSouth : directions.West;
            createTile_Road(row, column + j, d);
            d = isEntrance(row, column + j + 1, stationGenerator.coordinatesStationEntrances) ? lc.CounterClockwiseRingwayDirection ? directions.EastNorth : directions.NorthSouthWest : lc.CounterClockwiseRingwayDirection ? directions.East : directions.SouthWest;
            createTile_Road(row, column + j + 1, d);
            d = lc.CounterClockwiseRingwayDirection ? directions.SouthWest : directions.EastNorth;
            createTile_Road(row + 1, column + j, d);
            d = lc.CounterClockwiseRingwayDirection ? directions.NorthWest : directions.EastSouth;
            createTile_Road(row + 1, column + j + 1, d);
        }

        //the corners
        d = isEntrance(row, column, stationGenerator.coordinatesStationEntrances) ? lc.CounterClockwiseRingwayDirection ? directions.EastNorth : directions.NorthSouth : lc.CounterClockwiseRingwayDirection ? directions.East : directions.South;
        createTile_Road(row, column, d);
        d = lc.CounterClockwiseRingwayDirection ? directions.North : directions.EastSouth;
        createTile_Road(row + 1, column, d);
        d = isEntrance(row, column + length - 1, stationGenerator.coordinatesStationEntrances) ? lc.CounterClockwiseRingwayDirection ? directions.NorthSouth : directions.NorthWest : lc.CounterClockwiseRingwayDirection ? directions.South : directions.West;
        createTile_Road(row, column + length - 1, d);
        d = lc.CounterClockwiseRingwayDirection ? directions.SouthWest : directions.North;
        createTile_Road(row + 1, column + length - 1, d);
    }

    public void createDirectionsForSpaceSouthOfStorageArea(int row, int column, int length, int width)
    {
        if (length % 2 != 0)
        {
            throw new ArgumentException("createSpaceSouthOfStorageArea, length %2 != 0, length: " + length);
        }

        //the main part of this space
        var d = lc.CounterClockwiseRingwayDirection ? directions.North : directions.South;
        for (var j = 0; j < length; j++)
        {
            for (var i = 0; i < width - 2; i++)
            {
                createTile_Road(row + i, column + j, d);
            }
            d = d.Equals(directions.North) ? directions.South : directions.North;
        }

        //side at the workstations
        for (var j = 1; j < length - 1; j += 2)
        {
            d = lc.CounterClockwiseRingwayDirection ? directions.EastSouth : directions.NorthWest;
            createTile_Road(row + width - 2, column + j, d);
            d = lc.CounterClockwiseRingwayDirection ? directions.EastNorth : directions.SouthWest;
            createTile_Road(row + width - 2, column + j + 1, d);
            d = isEntrance(row + width - 1, column + j, stationGenerator.coordinatesStationEntrances) ? lc.CounterClockwiseRingwayDirection ? directions.SouthWest : directions.EastNorthSouth : lc.CounterClockwiseRingwayDirection ? directions.West : directions.EastNorth;
            createTile_Road(row + width - 1, column + j, d);
            d = isEntrance(row + width - 1, column + j + 1, stationGenerator.coordinatesStationEntrances) ? lc.CounterClockwiseRingwayDirection ? directions.NorthSouthWest : directions.EastSouth : lc.CounterClockwiseRingwayDirection ? directions.NorthWest : directions.East;
            createTile_Road(row + width - 1, column + j + 1, d);
        }

        //the corners
        d = lc.CounterClockwiseRingwayDirection ? directions.EastNorth : directions.South;
        createTile_Road(row + width - 2, column, d);
        d = isEntrance(row + width - 1, column, stationGenerator.coordinatesStationEntrances) ? lc.CounterClockwiseRingwayDirection ? directions.NorthSouth : directions.EastSouth : lc.CounterClockwiseRingwayDirection ? directions.North : directions.East;
        createTile_Road(row + width - 1, column, d);
        d = lc.CounterClockwiseRingwayDirection ? directions.South : directions.NorthWest;
        createTile_Road(row + width - 2, column + length - 1, d);
        d = isEntrance(row + width - 1, column + length - 1, stationGenerator.coordinatesStationEntrances) ? lc.CounterClockwiseRingwayDirection ? directions.SouthWest : directions.NorthSouth : lc.CounterClockwiseRingwayDirection ? directions.West : directions.North;
        createTile_Road(row + width - 1, column + length - 1, d);
    }

    public void createHighwayHallway(HallwayField field)
    {
        // Small sanity check
        if (lc.WidthHall % 2 != 0)
            throw new ArgumentException("createHighwayHallway, invalid width of hallway, lc.WidthHall %2 != 0, lc.WidthHall: " + lc.WidthHall);

        // Init
        List<int> rowIndices = null; List<int> columnIndices = null;
        Func<bool> firstRowForward = null; Func<bool> firstColumnForward = null;
        Func<int, int, bool, bool, directions> getDirection = null;

        // Setup depending on field to generate
        switch (field)
        {
            case HallwayField.East:
            {
                var firstRow = northWestCornerStorageAreaRow();
                var firstColumn = northWestCornerStorageAreaColumn() + lc.lengthStorageArea();
                var lastRow = northWestCornerStorageAreaRow() + lc.widthStorageArea() + (lc.hasStationsSouth() ? lc.WidthHall : 0) - 1;
                var lastColumn = northWestCornerStorageAreaColumn() + lc.lengthStorageArea() + lc.WidthHall - 1;
                rowIndices = new List<int>(Enumerable.Range(
                    firstRow,
                    lastRow - firstRow + 1));
                columnIndices = new List<int>(Enumerable.Range(
                    firstColumn,
                    lastColumn - firstColumn + 1));
                firstRowForward = () => { return lc.CounterClockwiseRingwayDirection; };
                firstColumnForward = () => { return !lc.CounterClockwiseRingwayDirection; };
                getDirection = (int i, int j, bool fRow, bool fCol) =>
                {
                    return LayoutGenerator.GetDirectionType(
                        // East connection?
                        (fRow && j != lastColumn) || isEntrance(i, j, stationGenerator.coordinatesStationEntrances),
                        // West connection?
                        !fRow,
                        // South connection?
                        fCol && i != lastRow,
                        // North connection?
                        !fCol && (i != firstRow || lc.hasStationsNorth()));
                };
            }
                break;
            case HallwayField.West:
            {
                var firstRow = northWestCornerStorageAreaRow() + lc.widthStorageArea() - 1;
                var firstColumn = northWestCornerStorageAreaColumn() - 1;
                var lastRow = northWestCornerStorageAreaRow() - (lc.hasStationsNorth() ? lc.WidthHall : 0);
                var lastColumn = northWestCornerStorageAreaColumn() - lc.WidthHall;
                rowIndices = new List<int>(Enumerable.Range(
                    lastRow,
                    firstRow - lastRow + 1).Reverse());
                columnIndices = new List<int>(Enumerable.Range(
                    lastColumn,
                    firstColumn - lastColumn + 1).Reverse());
                firstRowForward = () => { return lc.CounterClockwiseRingwayDirection; };
                firstColumnForward = () => { return !lc.CounterClockwiseRingwayDirection; };
                getDirection = (int i, int j, bool fRow, bool fCol) =>
                {
                    return LayoutGenerator.GetDirectionType(
                        // East connection?
                        !fRow,
                        // West connection?
                        (fRow && j != lastColumn) || isEntrance(i, j, stationGenerator.coordinatesStationEntrances),
                        // South connection?
                        !fCol && (i != firstRow || lc.hasStationsSouth()),
                        // North connection?
                        fCol && i != lastRow);
                };
            }
                break;
            case HallwayField.South:
            {
                // Attention: rows and columns swapped!
                var firstRow = northWestCornerStorageAreaRow() + lc.widthStorageArea();
                var firstColumn = northWestCornerStorageAreaColumn() + lc.lengthStorageArea() - 1;
                var lastRow = northWestCornerStorageAreaRow() + lc.widthStorageArea() + lc.WidthHall - 1;
                var lastColumn = northWestCornerStorageAreaColumn() - (lc.hasStationsWest() ? lc.WidthHall : 0);
                rowIndices = new List<int>(Enumerable.Range(
                    firstRow,
                    lastRow - firstRow + 1));
                columnIndices = new List<int>(Enumerable.Range(
                    lastColumn,
                    firstColumn - lastColumn + 1).Reverse());
                firstRowForward = () => { return !lc.CounterClockwiseRingwayDirection; };
                firstColumnForward = () => { return lc.CounterClockwiseRingwayDirection; };
                getDirection = (int i, int j, bool fRow, bool fCol) =>
                {
                    return LayoutGenerator.GetDirectionType(
                        // East connection?
                        !fRow && (j != firstColumn || lc.hasStationsEast()),
                        // West connection?
                        fRow && j != lastColumn,
                        // South connection?
                        (fCol && i != lastRow) || isEntrance(i, j, stationGenerator.coordinatesStationEntrances),
                        // North connection?
                        !fCol);
                };
            }
                break;
            case HallwayField.North:
            {
                // Attention: rows and columns swapped!
                var firstRow = northWestCornerStorageAreaRow() - 1;
                var firstColumn = northWestCornerStorageAreaColumn();
                var lastRow = northWestCornerStorageAreaRow() - lc.WidthHall;
                var lastColumn = northWestCornerStorageAreaColumn() + lc.lengthStorageArea() + (lc.hasStationsEast() ? lc.WidthHall : 0) - 1;
                rowIndices = new List<int>(Enumerable.Range(
                    lastRow,
                    firstRow - lastRow + 1).Reverse());
                columnIndices = new List<int>(Enumerable.Range(
                    firstColumn,
                    lastColumn - firstColumn + 1));
                firstRowForward = () => { return !lc.CounterClockwiseRingwayDirection; };
                firstColumnForward = () => { return lc.CounterClockwiseRingwayDirection; };
                getDirection = (int i, int j, bool fRow, bool fCol) =>
                {
                    return LayoutGenerator.GetDirectionType(
                        // East connection?
                        fRow && j != lastColumn,
                        // West connection?
                        !fRow && (j != firstColumn || lc.hasStationsWest()),
                        // South connection?
                        !fCol,
                        // North connection?
                        (fCol && i != lastRow) || isEntrance(i, j, stationGenerator.coordinatesStationEntrances));
                };
            }
                break;
            default:
                break;
        }

        // Actually generate the tiles of the area
        var forwardRow = firstRowForward();
        foreach (var i in rowIndices)
        {
            var forwardColumn = firstColumnForward();
            foreach (var j in columnIndices)
            {
                var d = getDirection(i, j, forwardRow, forwardColumn);
                createTile_Road(i, j, d);
                forwardColumn = !forwardColumn;
            }
            forwardRow = !forwardRow;
        }
    }

    public void createRingWay(int rowNorthWestCornerRingway, int columnNorthWestCornerRingway)
    {
        createRingwaySides(rowNorthWestCornerRingway, columnNorthWestCornerRingway);
        createRingwayAllCorners(rowNorthWestCornerRingway, columnNorthWestCornerRingway);
    }

    public void createRingwaySides(int rowNorthWestCornerRingway, int columnNorthWestCornerRingway)
    {
        //north side of the ringway
        var size = lc.lengthStorageArea() - 2; //-2 as the corners are excluded
        var nAisles = lc.NrVerticalAisles;
        var blockLength = lc.HorizontalLengthBlock;
        var outwardPossible = lc.hasStationsNorth();
        var forward = lc.AislesTwoDirectional ? directions.EastSouthWest : lc.CounterClockwiseRingwayDirection ? directions.East : directions.West;
        var forwardAndInward = lc.AislesTwoDirectional ? directions.EastSouthWest : lc.CounterClockwiseRingwayDirection ? directions.EastSouth : directions.SouthWest;
        var forwardAndOutwards = lc.AislesTwoDirectional ? directions.EastNorthSouthWest : lc.CounterClockwiseRingwayDirection ? directions.EastNorth : directions.NorthWest;
        var forwardsInwardsAndOutwards = lc.AislesTwoDirectional ? directions.EastNorthSouthWest : lc.CounterClockwiseRingwayDirection ? directions.EastNorthSouth : directions.NorthSouthWest;
        var row = rowNorthWestCornerRingway;
        var column = columnNorthWestCornerRingway + 1;
        var horizontal = true;
        var firstAisleOutward = !lc.CounterClockwiseRingwayDirection;
        createRingwaySideWithoutCorners(firstAisleOutward, size, outwardPossible, nAisles, blockLength, forward, forwardAndInward, forwardAndOutwards, forwardsInwardsAndOutwards, row, column, horizontal);

        //south side of the ringway
        size = lc.lengthStorageArea() - 2; //-2 as the corners are excluded
        nAisles = lc.NrVerticalAisles;
        blockLength = lc.HorizontalLengthBlock;
        outwardPossible = lc.hasStationsSouth();
        forward = lc.AislesTwoDirectional ? directions.EastNorthWest : lc.CounterClockwiseRingwayDirection ? directions.West : directions.East;
        forwardAndInward = lc.AislesTwoDirectional ? directions.EastNorthWest : lc.CounterClockwiseRingwayDirection ? directions.NorthWest : directions.EastNorth;
        forwardAndOutwards = lc.AislesTwoDirectional ? directions.EastNorthSouthWest : lc.CounterClockwiseRingwayDirection ? directions.SouthWest : directions.EastSouth;
        forwardsInwardsAndOutwards = lc.AislesTwoDirectional ? directions.EastNorthSouthWest : lc.CounterClockwiseRingwayDirection ? directions.NorthSouthWest : directions.EastNorthSouth;
        row = rowNorthWestCornerRingway + lc.widthStorageArea() - 1;
        column = columnNorthWestCornerRingway + 1;
        horizontal = true;
        firstAisleOutward = lc.CounterClockwiseRingwayDirection;
        createRingwaySideWithoutCorners(firstAisleOutward, size, outwardPossible, nAisles, blockLength, forward, forwardAndInward, forwardAndOutwards, forwardsInwardsAndOutwards, row, column, horizontal);

        //west side of the ringway
        size = lc.widthStorageArea() - 2; //-2 as the corners are excluded
        nAisles = lc.NrHorizontalAisles;
        blockLength = lc.VerticalLengthBlock;
        outwardPossible = lc.hasStationsWest();
        forward = lc.AislesTwoDirectional ? directions.EastNorthSouth : lc.CounterClockwiseRingwayDirection ? directions.North : directions.South;
        forwardAndInward = lc.AislesTwoDirectional ? directions.EastNorthSouth : lc.CounterClockwiseRingwayDirection ? directions.EastNorth : directions.EastSouth;
        forwardAndOutwards = lc.AislesTwoDirectional ? directions.EastNorthSouthWest : lc.CounterClockwiseRingwayDirection ? directions.NorthWest : directions.SouthWest;
        forwardsInwardsAndOutwards = lc.AislesTwoDirectional ? directions.EastNorthSouthWest : lc.CounterClockwiseRingwayDirection ? directions.EastNorthWest : directions.EastSouthWest;
        row = rowNorthWestCornerRingway + 1;
        column = columnNorthWestCornerRingway;
        horizontal = false;
        firstAisleOutward = lc.CounterClockwiseRingwayDirection;
        createRingwaySideWithoutCorners(firstAisleOutward, size, outwardPossible, nAisles, blockLength, forward, forwardAndInward, forwardAndOutwards, forwardsInwardsAndOutwards, row, column, horizontal);

        //east side of the ringway
        size = lc.widthStorageArea() - 2; //-2 as the corners are excluded
        nAisles = lc.NrHorizontalAisles;
        blockLength = lc.VerticalLengthBlock;
        outwardPossible = lc.hasStationsEast();
        forward = lc.AislesTwoDirectional ? directions.NorthSouthWest : lc.CounterClockwiseRingwayDirection ? directions.South : directions.North;
        forwardAndInward = lc.AislesTwoDirectional ? directions.NorthSouthWest : lc.CounterClockwiseRingwayDirection ? directions.SouthWest : directions.NorthWest;
        forwardAndOutwards = lc.AislesTwoDirectional ? directions.EastNorthSouthWest : lc.CounterClockwiseRingwayDirection ? directions.EastSouth : directions.EastNorth;
        forwardsInwardsAndOutwards = lc.AislesTwoDirectional ? directions.EastNorthSouthWest : lc.CounterClockwiseRingwayDirection ? directions.EastSouthWest : directions.EastNorthWest;
        row = rowNorthWestCornerRingway + 1;
        column = columnNorthWestCornerRingway + lc.lengthStorageArea() - 1;
        horizontal = false;
        firstAisleOutward = !lc.CounterClockwiseRingwayDirection;
        createRingwaySideWithoutCorners(firstAisleOutward, size, outwardPossible, nAisles, blockLength, forward, forwardAndInward, forwardAndOutwards, forwardsInwardsAndOutwards, row, column, horizontal);
    }

    public void createRingwaySideWithoutCorners(bool firstAisleOutward, int size, bool outwardPossible, int nAisles, int blockLength, directions forward, directions forwardAndInward, directions forwardAndOutwards, directions forwardsInwardsAndOutwards, int row, int column, bool horizontal)
    {
        var outward = createRingwaySideWithoutCorners_creatOutwardPossible(size, outwardPossible, firstAisleOutward);
        var inward = createRingwaySideWithoutCorners_creatInwardPossible(size, blockLength, nAisles, firstAisleOutward);

        for (var i = 0; i < size; i++)
        {
            var d = outward[i] ? inward[i] ? forwardsInwardsAndOutwards : forwardAndOutwards : inward[i] ? forwardAndInward : forward;
            createTile_Road(row, column, d);
            if (horizontal) { column++; } else { row++; }
        }
    }

    public bool[] createRingwaySideWithoutCorners_creatInwardPossible(int size, int blockLength, int nAisles, bool firstAisleOutward)
    {
        var inward = new bool[size];
        var aisleInward = !firstAisleOutward;
        var count = 0;
        for (var aisle = 0; aisle < nAisles; aisle++)
        {
            for (var i = 0; i < blockLength; i++)
            {
                inward[count++] = true;
            }
            inward[count++] = aisleInward;
            aisleInward = !aisleInward;
            if (!lc.SingleLane)
            {
                inward[count++] = aisleInward;
                aisleInward = !aisleInward;
            }
        }
        for (var i = 0; i < blockLength; i++)
        {
            inward[count++] = true;
        }
        return inward;
    }

    public bool[] createRingwaySideWithoutCorners_creatOutwardPossible(int size, bool outwardPossible, bool firstAisleOutward)
    {
        var outward = new bool[size];
        if (!outwardPossible)
        {
            for (var i = 0; i < outward.Length; i++)
            {
                outward[i] = false;
            }
        }
        else
        {
            for (var i = 0; i < outward.Length - 1; i += 2)
            {
                if (lc.AislesTwoDirectional)
                {
                    outward[i] = true;
                    outward[i + 1] = true;
                }
                else
                {
                    outward[i] = firstAisleOutward;
                    outward[i + 1] = !outward[i];
                }
            }
        }
        return outward;
    }

    public void createRingwayAllCorners(int rowNorthWestCornerRingway, int columnNorthWestCornerRingway)
    {
        //north west corner
        var row = rowNorthWestCornerRingway;
        var column = columnNorthWestCornerRingway;
        var directionHorizontally = lc.CounterClockwiseRingwayDirection ? directions.East : directions.West;
        var directionVertically = lc.CounterClockwiseRingwayDirection ? directions.North : directions.South;
        var useDirectionHorizontally = lc.CounterClockwiseRingwayDirection ? true : lc.hasStationsWest();
        var useDirectionVertically = lc.CounterClockwiseRingwayDirection ? lc.hasStationsNorth() : true;
        if (lc.AislesTwoDirectional)
        {
            var d = lc.hasStationsWest() ? lc.hasStationsNorth() ? directions.EastNorthSouthWest : directions.EastSouthWest : lc.hasStationsNorth() ? directions.EastNorthSouth : directions.EastSouth;
            createTile_Road(row, column, d);
        }
        else
        {
            createRingwayCorner(row, column, directionHorizontally, directionVertically, useDirectionHorizontally, useDirectionVertically);
        }

        //north east corner
        row = rowNorthWestCornerRingway;
        column = columnNorthWestCornerRingway + lc.lengthStorageArea() - 1;
        directionHorizontally = lc.CounterClockwiseRingwayDirection ? directions.East : directions.West;
        directionVertically = lc.CounterClockwiseRingwayDirection ? directions.South : directions.North;
        useDirectionHorizontally = lc.CounterClockwiseRingwayDirection ? lc.hasStationsEast() : true;
        useDirectionVertically = lc.CounterClockwiseRingwayDirection ? true : lc.hasStationsNorth();
        if (lc.AislesTwoDirectional)
        {
            var d = lc.hasStationsEast() ? lc.hasStationsNorth() ? directions.EastNorthSouthWest : directions.EastSouthWest : lc.hasStationsNorth() ? directions.NorthSouthWest : directions.SouthWest;
            createTile_Road(row, column, d);
        }
        else
        {
            createRingwayCorner(row, column, directionHorizontally, directionVertically, useDirectionHorizontally, useDirectionVertically);
        }

        //south west corner
        row = rowNorthWestCornerRingway + lc.widthStorageArea() - 1;
        column = columnNorthWestCornerRingway;
        directionHorizontally = lc.CounterClockwiseRingwayDirection ? directions.West : directions.East;
        directionVertically = lc.CounterClockwiseRingwayDirection ? directions.North : directions.South;
        useDirectionHorizontally = lc.CounterClockwiseRingwayDirection ? lc.hasStationsWest() : true;
        useDirectionVertically = lc.CounterClockwiseRingwayDirection ? true : lc.hasStationsSouth();
        if (lc.AislesTwoDirectional)
        {
            var d = lc.hasStationsWest() ? lc.hasStationsSouth() ? directions.EastNorthSouthWest : directions.EastNorthWest : lc.hasStationsSouth() ? directions.EastNorthSouth : directions.EastNorth;
            createTile_Road(row, column, d);
        }
        else
        {
            createRingwayCorner(row, column, directionHorizontally, directionVertically, useDirectionHorizontally, useDirectionVertically);
        }

        //south east corner
        row = rowNorthWestCornerRingway + lc.widthStorageArea() - 1;
        column = columnNorthWestCornerRingway + lc.lengthStorageArea() - 1;
        directionHorizontally = lc.CounterClockwiseRingwayDirection ? directions.West : directions.East;
        directionVertically = lc.CounterClockwiseRingwayDirection ? directions.South : directions.North;
        useDirectionHorizontally = lc.CounterClockwiseRingwayDirection ? true : lc.hasStationsEast();
        useDirectionVertically = lc.CounterClockwiseRingwayDirection ? lc.hasStationsSouth() : true;
        if (lc.AislesTwoDirectional)
        {
            var d = lc.hasStationsEast() ? lc.hasStationsSouth() ? directions.EastNorthSouthWest : directions.EastNorthWest : lc.hasStationsSouth() ? directions.NorthSouthWest : directions.NorthWest;
            createTile_Road(row, column, d);
        }
        else
        {
            createRingwayCorner(row, column, directionHorizontally, directionVertically, useDirectionHorizontally, useDirectionVertically);
        }
    }

    public void createRingwayCorner(int row, int column, directions directionHorizontally, directions directionVertically, bool useDirectionHorizontally, bool useDirectionVertically)
    {
        if (useDirectionHorizontally && useDirectionVertically)
        {
            var d = directionHorizontally.Equals(directions.West) ? directionVertically.Equals(directions.North) ? directions.NorthWest : directions.SouthWest : directionVertically.Equals(directions.North) ? directions.EastNorth : directions.EastSouth;
            createTile_Road(row, column, d);
        }
        else if (useDirectionHorizontally)
        {
            createTile_Road(row, column, directionHorizontally);
        }
        else if (useDirectionVertically)
        {
            createTile_Road(row, column, directionVertically);
        }
        else
        {
            createTile_Road(row, column, directions.Invalid);
        }
    }

    public void createRowOfStorageBlock(ref int row, ref int column)
    {
        for (var storageLocation = 0; storageLocation < lc.HorizontalLengthBlock; storageLocation++)
        {
            createTile_StorageLocation(row, column++, directions.EastNorthSouthWest);
        }
    }

    public void createRowWithStorageLocations(int row, int column)
    {
        var d = lc.AislesTwoDirectional ? directions.EastNorthSouthWest : lc.CounterClockwiseRingwayDirection ? directions.EastSouthWest : directions.EastNorthWest;
        for (var crossAisle = 0; crossAisle < lc.NrVerticalAisles; crossAisle++)
        {
            createRowOfStorageBlock(ref row, ref column); //storage locations to the left of the cross-aisle
            createTile_Road(row, column++, d); //the cross aisle segment;
            if (!lc.SingleLane)
            {
                d = lc.AislesTwoDirectional ? directions.EastNorthSouthWest : d.Equals(directions.EastSouthWest) ? directions.EastNorthWest : directions.EastSouthWest;
                createTile_Road(row, column++, d); //an additional cross aisle segment;
            }
            if (crossAisle == lc.NrVerticalAisles - 1)
            {
                createRowOfStorageBlock(ref row, ref column); //if it is the last cross-aisle, it also need to create the storage locations to its right since no other cross-aisle will do that
            }
            d = lc.AislesTwoDirectional ? directions.EastNorthSouthWest : d.Equals(directions.EastSouthWest) ? directions.EastNorthWest : directions.EastSouthWest;
        }
    }

    public void createStorageAreaAndHalls()
    {
        var row = northWestCornerStorageAreaRow();
        var column = northWestCornerStorageAreaColumn();
        createStorageArea(row, column);
        switch (lc.AisleLayoutType)
        {
            case AisleLayoutTypes.Tim:
            {
                if (lc.hasStationsEast())
                {
                    row = northWestCornerStorageAreaRow();
                    column = northWestCornerStorageAreaColumn() + lc.lengthStorageArea();
                    var length = lc.WidthHall;
                    var width = lc.widthStorageArea();
                    if (lc.AislesTwoDirectional)
                    {
                        createSpaceBetweenStorageAreaAndStations_TwoDirectionalCase(row, column, length, width, directions.West, stationGenerator.coordinatesStationEntrances);
                    }
                    else
                    {
                        createDirectionsForSpaceEastOfStorageArea(row, column, length, width);
                    }
                }
                if (lc.hasStationsWest())
                {
                    row = northWestCornerStorageAreaRow();
                    column = lc.WidthBuffer;
                    var length = lc.WidthHall;
                    var width = lc.widthStorageArea();
                    if (lc.AislesTwoDirectional)
                    {
                        createSpaceBetweenStorageAreaAndStations_TwoDirectionalCase(row, column, length, width, directions.East, stationGenerator.coordinatesStationEntrances);
                    }
                    else
                    {
                        createDirectionsForSpaceWestOfStorageArea(row, column, length, width);
                    }
                }
                if (lc.hasStationsNorth())
                {
                    row = lc.WidthBuffer;
                    column = northWestCornerStorageAreaColumn();
                    var length = lc.lengthStorageArea();
                    var width = lc.WidthHall;
                    if (lc.AislesTwoDirectional)
                    {
                        createSpaceBetweenStorageAreaAndStations_TwoDirectionalCase(row, column, length, width, directions.South, stationGenerator.coordinatesStationEntrances);
                    }
                    else
                    {
                        createDirectionsForSpaceNorthOfStorageArea(row, column, length, width);
                    }
                }
                if (lc.hasStationsSouth())
                {
                    row = northWestCornerStorageAreaRow() + lc.widthStorageArea();
                    column = northWestCornerStorageAreaColumn();
                    var length = lc.lengthStorageArea();
                    var width = lc.WidthHall;
                    if (lc.AislesTwoDirectional)
                    {
                        createSpaceBetweenStorageAreaAndStations_TwoDirectionalCase(row, column, length, width, directions.North, stationGenerator.coordinatesStationEntrances);
                    }
                    else
                    {
                        createDirectionsForSpaceSouthOfStorageArea(row, column, length, width);
                    }
                }
            }
                break;
            case AisleLayoutTypes.HighwayHallway:
            {
                if (lc.hasStationsEast())
                    createHighwayHallway(HallwayField.East);
                if (lc.hasStationsWest())
                    createHighwayHallway(HallwayField.West);
                if (lc.hasStationsNorth())
                    createHighwayHallway(HallwayField.North);
                if (lc.hasStationsSouth())
                    createHighwayHallway(HallwayField.South);
            }
                break;
            default:
                break;
        }
    }

    public void createSpaceBetweenStorageAreaAndStations_TwoDirectionalCase(int startRow, int startColumn, int length, int width, directions sideWithFourDirections, HashSet<Coordinate> coordinatesEntrances)
    {
        if (!sideWithFourDirections.Equals(directions.East) && !sideWithFourDirections.Equals(directions.West) && !sideWithFourDirections.Equals(directions.South) && !sideWithFourDirections.Equals(directions.North))
        {
            throw new ArgumentException("sideOfHallWithFourDirections is not east, north, west or south as it should be, sideOfHallWithFourDirections: " + sideWithFourDirections);
        }

        //inner part
        var d = directions.EastNorthSouthWest;
        for (var row = startRow + 1; row < startRow + width - 1; row++)
        {
            for (var column = startColumn + 1; column < startColumn + length - 1; column++)
            {
                createTile_Road(row, column, d);
            }
        }

        //north parth without corners
        for (var column = startColumn + 1; column < startColumn + length - 1; column++)
        {
            d = isEntrance(startRow, column, coordinatesEntrances) || sideWithFourDirections.Equals(directions.North) ? directions.EastNorthSouthWest : directions.EastSouthWest;
            createTile_Road(startRow, column, d);
        }

        //south parth without corners
        for (var column = startColumn + 1; column < startColumn + length - 1; column++)
        {
            var row = startRow + width - 1;
            d = isEntrance(row, column, coordinatesEntrances) || sideWithFourDirections.Equals(directions.South) ? directions.EastNorthSouthWest : directions.EastNorthWest;
            createTile_Road(row, column, d);
        }

        //west parth without corners
        for (var row = startRow + 1; row < startRow + width - 1; row++)
        {
            d = isEntrance(row, startColumn, coordinatesEntrances) || sideWithFourDirections.Equals(directions.West) ? directions.EastNorthSouthWest : directions.EastNorthSouth;
            createTile_Road(row, startColumn, d);
        }

        //east parth without corners
        for (var row = startRow + 1; row < startRow + width - 1; row++)
        {
            var column = startColumn + length - 1;
            d = isEntrance(row, column, coordinatesEntrances) || sideWithFourDirections.Equals(directions.East) ? directions.EastNorthSouthWest : directions.NorthSouthWest;
            createTile_Road(row, column, d);
        }

        //north west corner
        var rowCorner = startRow;
        var columnCorner = startColumn;
        d = sideWithFourDirections.Equals(directions.West) ? directions.EastSouthWest : sideWithFourDirections.Equals(directions.North) ? directions.EastNorthSouth : directions.EastSouth;
        createTile_Road(rowCorner, columnCorner, d);

        //north east corner
        rowCorner = startRow;
        columnCorner = startColumn + length - 1;
        d = sideWithFourDirections.Equals(directions.East) ? directions.EastSouthWest : sideWithFourDirections.Equals(directions.North) ? directions.NorthSouthWest : directions.SouthWest;
        createTile_Road(rowCorner, columnCorner, d);

        //south west corner
        rowCorner = startRow + width - 1;
        columnCorner = startColumn;
        d = sideWithFourDirections.Equals(directions.West) ? directions.EastNorthWest : sideWithFourDirections.Equals(directions.South) ? directions.EastNorthSouth : directions.EastNorth;
        createTile_Road(rowCorner, columnCorner, d);

        //south east corner
        rowCorner = startRow + width - 1;
        columnCorner = startColumn + length - 1;
        d = sideWithFourDirections.Equals(directions.East) ? directions.EastNorthWest : sideWithFourDirections.Equals(directions.South) ? directions.NorthSouthWest : directions.NorthWest;
        createTile_Road(rowCorner, columnCorner, d);
    }

    public void createStorageArea(int row, int column)
    {
        createRingWay(row++, column++);
        var directionHorizontally = lc.AislesTwoDirectional ? directions.EastNorthSouthWest : lc.CounterClockwiseRingwayDirection ? directions.NorthSouthWest : directions.EastNorthSouth;
        var directionCrossPoint = lc.AislesTwoDirectional ? directions.EastNorthSouthWest : lc.CounterClockwiseRingwayDirection ? directions.SouthWest : directions.EastNorth;

        for (var aisle = 0; aisle < lc.NrHorizontalAisles; aisle++)
        {
            //create the storage locations north of the aisle
            createRowWithStorageLocations(row++, column);
            createRowWithStorageLocations(row++, column);

            //create the aisle itself
            createAisleHorizontally(row++, column, directionHorizontally, directionCrossPoint);
            if (!lc.SingleLane)
            {
                directionHorizontally = lc.AislesTwoDirectional ? directions.EastNorthSouthWest : directionHorizontally.Equals(directions.NorthSouthWest) ? directions.EastNorthSouth : directions.NorthSouthWest;
                directionCrossPoint = lc.AislesTwoDirectional ? directions.EastNorthSouthWest : directionCrossPoint.Equals(directions.SouthWest) ? directions.EastSouth : directionCrossPoint.Equals(directions.NorthWest) ? directions.EastNorth : directionCrossPoint.Equals(directions.EastSouth) ? directions.SouthWest : directionCrossPoint.Equals(directions.EastNorth) ? directions.NorthWest : directions.Invalid;
                createAisleHorizontally(row++, column, directionHorizontally, directionCrossPoint); //an additional aisle segment
            }

            if (aisle == lc.NrHorizontalAisles - 1)
            { //the last horizontal aisle has to create the storage locations to its south as there is no other aisle that will do that
                createRowWithStorageLocations(row++, column);
                createRowWithStorageLocations(row++, column);
            }
            directionHorizontally = lc.AislesTwoDirectional ? directions.EastNorthSouthWest : directionHorizontally.Equals(directions.NorthSouthWest) ? directions.EastNorthSouth : directions.NorthSouthWest;
            directionCrossPoint = lc.AislesTwoDirectional ? directions.EastNorthSouthWest : directionCrossPoint.Equals(directions.SouthWest) ? directions.EastSouth : directionCrossPoint.Equals(directions.NorthWest) ? directions.EastNorth : directionCrossPoint.Equals(directions.EastSouth) ? directions.SouthWest : directionCrossPoint.Equals(directions.EastNorth) ? directions.NorthWest : directions.Invalid;
        }
    }

    public void createTile_StorageLocation(int row, int column, directions d)
    {
        var wp = instance.CreateWaypoint(instance.RegisterWaypointID(), tier, column + 0.5, row + 0.5, true, false);
        if (tiles[row, column] != null)
        {
            throw new ArgumentException("trying to overwrite an existing waypoint!! At createTile_StorageLocation: tile[row, column] != null");
        }
        tiles[row, column] = new Tile(d, wp, waypointTypes.StorageLocation);
    }

    public void createTile_Road(int row, int column, directions d)
    {
        var wp = instance.CreateWaypoint(instance.RegisterWaypointID(), tier, column + 0.5, row + 0.5, false, false);
        if (tiles[row, column] != null)
        {
            throw new ArgumentException("trying to overwrite an existing waypoint!! At createTile_Road: tiles[row, column] != null");
        }
        tiles[row, column] = new Tile(d, wp, waypointTypes.Road);
    }

    public bool isEntrance(int row, int column, HashSet<Coordinate> coordinatesEntrances)
    {
        if (coordinatesEntrances.Contains(new Coordinate(row, column)))
            return true;
        return false;
    }

    public int northWestCornerStorageAreaRow()
    {
        return lc.hasStationsNorth() ? lc.WidthHall + lc.WidthBuffer : 0;
    }

    public int northWestCornerStorageAreaColumn()
    {
        return lc.hasStationsWest() ? lc.WidthHall + lc.WidthBuffer : 0;
    }

}