using RAWSimO.Core.Geometrics;
using RAWSimO.Core.IO;
using RAWSimO.Core.Statistics;
using RAWSimO.Toolbox;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RAWSimO.DataPreparation;

public class DataProcessor
{
    #region Core

    /// <summary>
    /// Creates a new <code>DataTransformer</code> instance.
    /// </summary>
    public DataProcessor() { Config = new PreparationConfiguration(); }

    /// <summary>
    /// The configuration to use.
    /// </summary>
    public PreparationConfiguration Config { get; set; }

    /// <summary>
    /// Prepares the results of all directories contained in the given directory.
    /// </summary>
    /// <param name="parentDir">The parent directory of the result sub-directories.</param>
    public void PrepareAllResults(string parentDir)
    {
        var dirs = Directory.EnumerateDirectories(parentDir).ToArray();
        var footprints = new Dictionary<string, string>();
        var instanceNames = new Dictionary<string, string>();
        var settingNames = new Dictionary<string, string>();
        var configNames = new Dictionary<string, string>();
        foreach (var dir in dirs)
        {
            // Skip dirs without the minimal files
            if (!File.Exists(Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.InstanceName])) ||
                !File.Exists(Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.SettingName])) ||
                !File.Exists(Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.ControllerName])))
                continue;
            // Fetch instance name
            var resultDir = Path.GetFileName(dir);
            using (var sr = new StreamReader(Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.InstanceName])))
            {
                var instanceNameLine = "";
                while (string.IsNullOrWhiteSpace(instanceNameLine = sr.ReadLine())) { /* Nothing to do - the head of the loop does the work */ }
                instanceNameLine = instanceNameLine.Trim();
                instanceNames[resultDir] = instanceNameLine;
            }
            // Fetch setting name
            using (var sr = new StreamReader(Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.SettingName])))
            {
                var settingNameLine = "";
                while (string.IsNullOrWhiteSpace(settingNameLine = sr.ReadLine())) { /* Nothing to do - the head of the loop does the work */ }
                settingNameLine = settingNameLine.Trim();
                settingNames[resultDir] = settingNameLine;
            }
            // Fetch config name
            using (var sr = new StreamReader(Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.ControllerName])))
            {
                var configNameLine = "";
                while (string.IsNullOrWhiteSpace(configNameLine = sr.ReadLine())) { /* Nothing to do - the head of the loop does the work */ }
                configNameLine = configNameLine.Trim();
                configNames[resultDir] = configNameLine;
            }
            // Prepare consolidated results
            var simulationDuration = double.NaN;
            if (File.Exists(Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.Footprint])))
            {
                using (var sr = new StreamReader(Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.Footprint])))
                {
                    var footPrintLine = "";
                    while (string.IsNullOrWhiteSpace(footPrintLine = sr.ReadLine())) { /* Nothing to do - the head of the loop does the work */ }
                    footprints[resultDir] = footPrintLine;
                    // Fetch time horizon - necessary information to build appropriate progression files
                    var fp = new FootprintDatapoint(footPrintLine);
                    simulationDuration = (double)fp[FootprintDatapoint.FootPrintEntry.Duration];
                }
            }
            // Prepare internal results (if there is a time horizon available)
            if (!double.IsNaN(simulationDuration))
            {
                PrepareResults(
                    dir, // The result dir
                    instanceNames[resultDir], // The real name of the instance
                    settingNames[resultDir], // The name of the setting
                    configNames[resultDir], // The name of the config
                    simulationDuration); // The simulation duration
            }
            // Plot information about the item descriptions used for the simulation
            InventoryInfoProcessor.PlotExecutionItemDescriptionInfo(dir, Config.Logger);
        }
        // Write consolidated result file
        Console.WriteLine("Writing consolidated result file.");
        using (var sw = new StreamWriter(Path.Combine(parentDir, IOConstants.STAT_CONSOLIDATED_FOOTPRINTS_FILENAME)))
        {
            sw.WriteLine(FootprintDatapoint.GetFootprintHeader());
            foreach (var kvp in footprints.OrderBy(k => k.Key))
            {
                sw.WriteLine(kvp.Value); // Add the result footprint
            }
        }
    }

    /// <summary>
    /// Condensates results of all directories contained in the given directory.
    /// </summary>
    /// <param name="parentDir">The parent directory of the result sub-directories.</param>
    public void PrepareOnlyFootprints(string parentDir)
    {
        var dirs = Directory.EnumerateDirectories(parentDir).ToArray();
        var footprints = new Dictionary<string, string>();
        var completeInstanceNames = new Dictionary<string, string>();
        var configNames = new Dictionary<string, string>();
        foreach (var dir in dirs)
        {
            // Fetch instance name
            var resultDir = Path.GetFileName(dir);
            if (File.Exists(Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.InstanceName])))
            {
                using (var sr = new StreamReader(Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.InstanceName])))
                {
                    var instanceNameLine = "";
                    while (string.IsNullOrWhiteSpace(instanceNameLine = sr.ReadLine())) { /* Nothing to do - the head of the loop does the work */ }
                    instanceNameLine = instanceNameLine.Trim();
                    completeInstanceNames[resultDir] = instanceNameLine;
                }
            }
            else
            {
                completeInstanceNames[resultDir] = resultDir;
            }
            // Fetch config name
            if (File.Exists(Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.SettingName])))
            {
                using (var sr = new StreamReader(Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.SettingName])))
                {
                    var configNameLine = "";
                    while (string.IsNullOrWhiteSpace(configNameLine = sr.ReadLine())) { /* Nothing to do - the head of the loop does the work */ }
                    configNameLine = configNameLine.Trim();
                    configNames[resultDir] = configNameLine;
                }
            }
            // Prepare consolidated results
            var timeHorizon = double.NaN;
            if (File.Exists(Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.Footprint])))
            {
                using (var sr = new StreamReader(Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.Footprint])))
                {
                    var footPrintLine = "";
                    while (string.IsNullOrWhiteSpace(footPrintLine = sr.ReadLine())) { /* Nothing to do - the head of the loop does the work */ }
                    footprints[resultDir] = footPrintLine;
                    // Fetch time horizon - necessary information to build appropriate progression files
                    var fp = new FootprintDatapoint(footPrintLine);
                    timeHorizon = (double)fp[FootprintDatapoint.FootPrintEntry.Duration];
                }
            }
        }
        // Write consolidated result file
        Console.WriteLine("Writing consolidated result file.");
        using (var sw = new StreamWriter(Path.Combine(parentDir, IOConstants.STAT_CONSOLIDATED_FOOTPRINTS_FILENAME)))
        {
            sw.WriteLine(FootprintDatapoint.GetFootprintHeader());
            foreach (var kvp in footprints.OrderBy(k => k.Key))
            {
                sw.WriteLine(kvp.Value); // Add the result footprint
            }
        }
    }

    /// <summary>
    /// Prepares all results of one evaluation run.
    /// </summary>
    /// <param name="path">The path to the directory containing all the evaluation results of one run.</param>
    /// <param name="instanceName">The name of the instance.</param>
    /// <param name="settingName">The name of the setting.</param>
    /// <param name="configName">The name of the config.</param>
    /// <param name="simulationDuration">The overall duration of the simulation.</param>
    public void PrepareResults(string path, string instanceName, string settingName, string configName, double simulationDuration)
    {
        // Log
        Config.Logger("Preparing results for " + Path.GetFileName(path) + ":");
        // Prepare item progression data (if available)
        if (File.Exists(Path.Combine(path, IOConstants.StatFileNames[IOConstants.StatFile.ItemProgressionRaw])))
        {
            Config.Logger("Preparing item progression ...");
            try { PrepareItemProgression(path, Path.Combine(path, IOConstants.StatFileNames[IOConstants.StatFile.ItemProgressionRaw]), simulationDuration); }
            catch (OutOfMemoryException) { Config.Logger("Error: Couldn't prepare the data due to memory limitations!"); }
        }
        // Prepare bundle progression data (if available)
        if (File.Exists(Path.Combine(path, IOConstants.StatFileNames[IOConstants.StatFile.BundleProgressionRaw])))
        {
            Config.Logger("Preparing bundle progression ...");
            try { PrepareBundleProgression(path, Path.Combine(path, IOConstants.StatFileNames[IOConstants.StatFile.BundleProgressionRaw]), simulationDuration); }
            catch (OutOfMemoryException) { Config.Logger("Error: Couldn't prepare the data due to memory limitations!"); }
        }
        // Prepare order progression data (if available)
        if (File.Exists(Path.Combine(path, IOConstants.StatFileNames[IOConstants.StatFile.OrderProgressionRaw])))
        {
            Config.Logger("Preparing order progression ...");
            try { PrepareOrderProgression(path, Path.Combine(path, IOConstants.StatFileNames[IOConstants.StatFile.OrderProgressionRaw]), simulationDuration); }
            catch (OutOfMemoryException) { Config.Logger("Error: Couldn't prepare the data due to memory limitations!"); }
        }
        // Prepare collision progression data (if available)
        if (File.Exists(Path.Combine(path, IOConstants.StatFileNames[IOConstants.StatFile.CollisionProgressionRaw])))
        {
            Config.Logger("Preparing collision progression ...");
            try { PrepareCollisionProgression(path, Path.Combine(path, IOConstants.StatFileNames[IOConstants.StatFile.CollisionProgressionRaw]), simulationDuration); }
            catch (OutOfMemoryException) { Config.Logger("Error: Couldn't prepare the data due to memory limitations!"); }
        }
        // Prepare distance progression data (if available)
        if (File.Exists(Path.Combine(path, IOConstants.StatFileNames[IOConstants.StatFile.TraveledDistanceProgressionRaw])))
        {
            Config.Logger("Preparing distance progression ...");
            try { PrepareDistanceTraveledProgression(path, Path.Combine(path, IOConstants.StatFileNames[IOConstants.StatFile.TraveledDistanceProgressionRaw]), simulationDuration); }
            catch (OutOfMemoryException) { Config.Logger("Error: Couldn't prepare the data due to memory limitations!"); }
        }
        // Prepare fixed time progression data (if available)
        Config.Logger("Preparing fixed time progression ...");
        //try { 
        PrepareFixedTimeProgression(path, Config, simulationDuration);
        //}
        //catch (Exception ex) { Config.Logger("Error - couldn't prepare the data: " + ex.Message); }
        // Generate scripts
        Config.Logger("Generating progression scripts ...");
        var script = GenerateProgressionScript(Config, path, instanceName, configName, false, false, false, false, false, false, false, false, true);
        var customScripts = GenerateCustomScript(path, instanceName, settingName, configName, Config.CustomDiagrams, simulationDuration).ToList();
        // Execute scripts
        ExecuteScript(script, Config.Logger);
        foreach (var customScript in customScripts)
            ExecuteScript(customScript, Config.Logger);
    }

    #endregion

    #region ItemProgression

    private void PrepareItemProgression(string outputDirPath, string itemProgressionDataPath, double timeHorizon)
    {
        // Read data
        var datapoints = new LinkedList<ItemHandledDatapoint>();
        using (var sr = new StreamReader(itemProgressionDataPath))
        {
            var line = "";
            while ((line = sr.ReadLine()) != null)
            {
                line = line.Trim();
                // Skip empty and comment lines
                if (line.StartsWith(IOConstants.COMMENT_LINE) || string.IsNullOrWhiteSpace(line))
                    continue;

                // Parse data
                datapoints.AddLast(new ItemHandledDatapoint(line));
            }
        }
        // Sort the values (just in case) and make a list that can be used for binary search
        var elements = datapoints.Select(d => d.TimeStamp).OrderBy(t => t).ToList();

        // Write prepared result files
        using (var sw = new StreamWriter(Path.Combine(outputDirPath, IOConstants.STAT_ITEM_PROGRESSION_RESULT_FILENAME + ".dat"), false))
        {
            // Write header
            sw.WriteLine("% time handles");

            // Write values
            var halfConsolidationLength = Config.ItemProgressionConsolidationTimespan / 2.0;
            for (double i = 0; i < timeHorizon + Config.ItemProgressionStepLength; i += Config.ItemProgressionStepLength)
            {
                var handles = CountElementsWithinRange(elements, i - halfConsolidationLength, true, i + halfConsolidationLength, false);
                sw.WriteLine(i.ToString(IOConstants.FORMATTER) + IOConstants.GNU_PLOT_VALUE_SPLIT + handles);
            }
        }
    }

    #endregion

    #region BundleProgression

    private void PrepareBundleProgression(string outputDirPath, string bundleProgressionDataPath, double timeHorizon)
    {
        // Read data
        var datapoints = new LinkedList<BundleHandledDatapoint>();
        using (var sr = new StreamReader(bundleProgressionDataPath))
        {
            var line = "";
            while ((line = sr.ReadLine()) != null)
            {
                line = line.Trim();
                // Skip empty and comment lines
                if (line.StartsWith(IOConstants.COMMENT_LINE) || string.IsNullOrWhiteSpace(line))
                    continue;

                // Parse data
                datapoints.AddLast(new BundleHandledDatapoint(line));
            }
        }
        // Sort the values (just in case) and make a list that can be used for binary search
        var elements = datapoints.Select(d => d.TimeStamp).OrderBy(t => t).ToList();

        // Write prepared result files
        using (var sw = new StreamWriter(Path.Combine(outputDirPath, IOConstants.STAT_BUNDLE_PROGRESSION_RESULT_FILENAME + ".dat"), false))
        {
            // Write header
            sw.WriteLine("% time handles");

            // Write values
            var halfConsolidationLength = Config.BundleProgressionConsolidationTimespan / 2.0;
            for (double i = 0; i < timeHorizon + Config.BundleProgressionStepLength; i += Config.BundleProgressionStepLength)
            {
                var handles = CountElementsWithinRange(elements, i - halfConsolidationLength, true, i + halfConsolidationLength, false);
                sw.WriteLine(i.ToString(IOConstants.FORMATTER) + IOConstants.GNU_PLOT_VALUE_SPLIT + handles);
            }
        }
    }

    #endregion

    #region OrderProgression

    private void PrepareOrderProgression(string outputDirPath, string orderProgressionDataPath, double timeHorizon)
    {
        // Read data
        var datapoints = new LinkedList<OrderHandledDatapoint>();
        using (var sr = new StreamReader(orderProgressionDataPath))
        {
            var line = "";
            while ((line = sr.ReadLine()) != null)
            {
                line = line.Trim();
                // Skip empty and comment lines
                if (line.StartsWith(IOConstants.COMMENT_LINE) || string.IsNullOrWhiteSpace(line))
                    continue;

                // Parse data
                datapoints.AddLast(new OrderHandledDatapoint(line));
            }
        }
        // Sort the values (just in case) and make a list that can be used for binary search
        var elements = datapoints.Select(d => d.TimeStamp).OrderBy(t => t).ToList();

        // Write prepared result files
        using (var sw = new StreamWriter(Path.Combine(outputDirPath, IOConstants.STAT_ORDER_PROGRESSION_RESULT_FILENAME + ".dat"), false))
        {
            // Write header
            sw.WriteLine("% time handles");

            // Write values
            var halfConsolidationLength = Config.OrderProgressionConsolidationTimespan / 2.0;
            for (double i = 0; i < timeHorizon + Config.OrderProgressionStepLength; i += Config.OrderProgressionStepLength)
            {
                var handles = CountElementsWithinRange(elements, i - halfConsolidationLength, true, i + halfConsolidationLength, false);
                sw.WriteLine(i.ToString(IOConstants.FORMATTER) + IOConstants.GNU_PLOT_VALUE_SPLIT + handles);
            }
        }
    }

    #endregion

    #region Collision progression

    private void PrepareCollisionProgression(string outputDirPath, string collisionProgressionDataPath, double timeHorizon)
    {
        // Read data
        var datapoints = new LinkedList<CollisionDatapoint>();
        using (var sr = new StreamReader(collisionProgressionDataPath))
        {
            var line = "";
            while ((line = sr.ReadLine()) != null)
            {
                line = line.Trim();
                // Skip empty and comment lines
                if (line.StartsWith(IOConstants.COMMENT_LINE) || string.IsNullOrWhiteSpace(line))
                    continue;

                // Parse data
                datapoints.AddLast(new CollisionDatapoint(line));
            }
        }
        // Sort the values (just in case) and make a list that can be used for binary search
        var elements = datapoints.Select(d => d.TimeStamp).OrderBy(t => t).ToList();

        // Write prepared result files
        using (var sw = new StreamWriter(Path.Combine(outputDirPath, IOConstants.STAT_COLLISION_PROGRESSION_RESULT_FILENAME + ".dat"), false))
        {
            // Write header
            sw.WriteLine("% time collisions");

            // Write values
            var halfConsolidationLength = Config.CollisionProgressionConsolidationTimespan / 2.0;
            for (double i = 0; i < timeHorizon + Config.CollisionProgressionStepLength; i += Config.CollisionProgressionStepLength)
            {
                var handles = CountElementsWithinRange(elements, i - halfConsolidationLength, true, i + halfConsolidationLength, false);
                sw.WriteLine(i.ToString(IOConstants.FORMATTER) + IOConstants.GNU_PLOT_VALUE_SPLIT + handles);
            }
        }
    }

    #endregion

    #region Distance traveled progression

    private void PrepareDistanceTraveledProgression(string outputDirPath, string distanceProgressionDataPath, double timeHorizon)
    {
        // Read data
        var datapoints = new LinkedList<DistanceDatapoint>();
        using (var sr = new StreamReader(distanceProgressionDataPath))
        {
            var line = "";
            while ((line = sr.ReadLine()) != null)
            {
                line = line.Trim();
                // Skip empty and comment lines
                if (line.StartsWith(IOConstants.COMMENT_LINE) || string.IsNullOrWhiteSpace(line))
                    continue;

                // Parse data
                datapoints.AddLast(new DistanceDatapoint(line));
            }
        }
        // Write prepared result files
        using (var sw = new StreamWriter(Path.Combine(outputDirPath, IOConstants.STAT_TRAVELED_DISTANCE_PROGRESSION_RESULT_FILENAME + ".dat"), false))
        {
            // Write header
            sw.WriteLine("% time distance");

            // Write values
            foreach (var value in datapoints.OrderBy(d => d.TimeStamp))
                sw.WriteLine(value.TimeStamp.ToString(IOConstants.FORMATTER) + IOConstants.GNU_PLOT_VALUE_SPLIT + value.DistanceTraveled.ToString(IOConstants.FORMATTER));
        }
    }

    #endregion

    #region Fixed time progression

    private void PrepareSingleFixedTimeProgression(string outputDirPath, CustomDiagramConfiguration customDiagram, double duration, PlotDataContentType dataType)
    {
        {
            // Prepare file paths
            string inputFile; var outputFile = Path.Combine(outputDirPath, IntermediateFileNamer(dataType, customDiagram.PlotTime, customDiagram.AverageWindow));
            inputFile = dataType switch
            {
                PlotDataContentType.OrdersHandled => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.OrderProgressionRaw]),
                PlotDataContentType.BundlesHandled => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.BundleProgressionRaw]),
                PlotDataContentType.ItemsHandled => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.ItemProgressionRaw]),
                PlotDataContentType.OrdersPlaced => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.OrderPlacementProgressionRaw]),
                PlotDataContentType.BundlesPlaced => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.BundlePlacementProgressionRaw]),
                PlotDataContentType.OrderThroughputTimeAvg => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.OrderProgressionRaw]),
                PlotDataContentType.OrderTurnoverTimeAvg => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.OrderProgressionRaw]),
                PlotDataContentType.OrderLatenessAvg => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.OrderProgressionRaw]),
                PlotDataContentType.LateOrderFractional => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.OrderProgressionRaw]),
                PlotDataContentType.BundleThroughputTimeAvg => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.BundleProgressionRaw]),
                PlotDataContentType.BundleTurnoverTimeAvg => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.BundleProgressionRaw]),
                PlotDataContentType.DistanceTraveled => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.TraveledDistanceProgressionRaw]),
                PlotDataContentType.LastMileTripTimeOStation => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.TripsCompletedProgressionRaw]),
                PlotDataContentType.LastMileTripTimeIStation => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.TripsCompletedProgressionRaw]),
                PlotDataContentType.BotsQueueing => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.BotInfoPollingRaw]),
                PlotDataContentType.TaskBotCounts => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.BotInfoPollingRaw]),
                PlotDataContentType.InventoryLevel => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.InventoryLevelPollingRaw]),
                PlotDataContentType.SKUCountContained => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.InventoryLevelPollingRaw]),
                PlotDataContentType.InvCombinedTotal => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.InventoryLevelPollingRaw]),
                PlotDataContentType.InvCombinedRank => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.InventoryLevelPollingRaw]),
                PlotDataContentType.InvCombinedAvgRank => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.InventoryLevelPollingRaw]),
                PlotDataContentType.InvSpeedTotal => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.InventoryLevelPollingRaw]),
                PlotDataContentType.InvSpeedRank => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.InventoryLevelPollingRaw]),
                PlotDataContentType.InvSpeedAvgRank => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.InventoryLevelPollingRaw]),
                PlotDataContentType.InvUtilityTotal => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.InventoryLevelPollingRaw]),
                PlotDataContentType.InvUtilityRank => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.InventoryLevelPollingRaw]),
                PlotDataContentType.InvUtilityAvgRank => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.InventoryLevelPollingRaw]),
                PlotDataContentType.MemoryUsage => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.PerformancePollingRaw]),
                PlotDataContentType.RealTime => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.PerformancePollingRaw]),
                PlotDataContentType.ControllerRuntimes => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.PerformancePollingRaw]),
                PlotDataContentType.OrdersBacklogLevel => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.BundleOrderSituationPollingRaw]),
                PlotDataContentType.BundlesBacklogLevel => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.BundleOrderSituationPollingRaw]),
                PlotDataContentType.OrderThroughputAgeAverage => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.BundleOrderSituationPollingRaw]),
                PlotDataContentType.BundleThroughputAgeAverage => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.BundleOrderSituationPollingRaw]),
                PlotDataContentType.OrderTurnoverAgeAverage => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.BundleOrderSituationPollingRaw]),
                PlotDataContentType.BundleTurnoverAgeAverage => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.BundleOrderSituationPollingRaw]),
                PlotDataContentType.OrderFrequencyAverage => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.BundleOrderSituationPollingRaw]),
                PlotDataContentType.BundleFrequencyAverage => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.BundleOrderSituationPollingRaw]),
                PlotDataContentType.PodsHandledAtIS => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.PodsHandledAtOS => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.BundlePileon => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.ItemPileon => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.IStationIdleTime => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.OStationIdleTime => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.IStationActiveTime => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.OStationActiveTime => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.IStationRequests => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.OStationRequests => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.IStationOpenWork => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.OStationOpenWork => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.IStationBots => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.OStationBots => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.IStationBundlesStored => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.OStationItemsPicked => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.IStationBundlePileon => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.OStationItemPileon => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.IStationsActive => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                PlotDataContentType.OStationsActive => Path.Combine(outputDirPath,
                    IOConstants.StatFileNames[IOConstants.StatFile.StationPollingRaw]),
                _ => throw new ArgumentException("Unknown content type: " + dataType)
            };
            // Get a sample line
            var sampleLine = File.ReadLines(inputFile).SkipWhile(l => l.StartsWith(IOConstants.COMMENT_LINE) || string.IsNullOrWhiteSpace(l)).FirstOrDefault();
            // Read the datapoints
            var datapoints = new List<GenericProgressionDatapoint>();
            using (var sr = new StreamReader(inputFile))
            {
                var currentLine = "";
                while ((currentLine = sr.ReadLine()) != null)
                {
                    currentLine = currentLine.Trim();
                    // Skip empty and comment lines
                    if (currentLine.StartsWith(IOConstants.COMMENT_LINE) || string.IsNullOrWhiteSpace(currentLine))
                        continue;
                    // Parse data
                    var elements = currentLine.Split(IOConstants.DELIMITER_VALUE);
                    var timestamp = double.Parse(elements[0], IOConstants.FORMATTER);
                    double[] singleDatapoint;
                    double[] singleSupportDatapoint1 = null;
                    switch (dataType)
                    {
                        case PlotDataContentType.OrdersHandled: singleDatapoint = [1]; break;
                        case PlotDataContentType.BundlesHandled: singleDatapoint = [1]; break;
                        case PlotDataContentType.ItemsHandled: singleDatapoint = [1]; break;
                        case PlotDataContentType.OrdersPlaced: singleDatapoint = [1]; break;
                        case PlotDataContentType.BundlesPlaced: singleDatapoint = [1]; break;
                        case PlotDataContentType.OrderThroughputTimeAvg: singleDatapoint = [new OrderHandledDatapoint(currentLine).ThroughputTime
                        ]; break;
                        case PlotDataContentType.OrderTurnoverTimeAvg: singleDatapoint = [new OrderHandledDatapoint(currentLine).TurnoverTime
                        ]; break;
                        case PlotDataContentType.OrderLatenessAvg: singleDatapoint = [new OrderHandledDatapoint(currentLine).Lateness
                        ]; break;
                        case PlotDataContentType.LateOrderFractional: singleDatapoint = [new OrderHandledDatapoint(currentLine).Lateness
                        ]; break;
                        case PlotDataContentType.BundleThroughputTimeAvg: singleDatapoint = [new BundleHandledDatapoint(currentLine).ThroughputTime
                        ]; break;
                        case PlotDataContentType.BundleTurnoverTimeAvg: singleDatapoint = [new BundleHandledDatapoint(currentLine).TurnoverTime
                        ]; break;
                        case PlotDataContentType.DistanceTraveled: singleDatapoint = [double.Parse(elements[1], IOConstants.FORMATTER)
                        ]; break;
                        case PlotDataContentType.LastMileTripTimeOStation:
                        {
                            var datapoint = new StationTripDatapoint(currentLine);
                            if (datapoint.Type != StationTripDatapoint.StationTripType.O)
                                continue;
                            singleDatapoint = [datapoint.TripTime];
                        }
                            break;
                        case PlotDataContentType.LastMileTripTimeIStation:
                        {
                            var datapoint = new StationTripDatapoint(currentLine);
                            if (datapoint.Type != StationTripDatapoint.StationTripType.I)
                                continue;
                            singleDatapoint = [datapoint.TripTime];
                        }
                            break;
                        case PlotDataContentType.BotsQueueing: singleDatapoint = [new BotDatapoint(currentLine).BotsQueueing
                        ]; break;
                        case PlotDataContentType.TaskBotCounts:
                        {
                            singleDatapoint = new BotDatapoint(currentLine).TaskBotCounts.OrderBy(t => t.Item1).Select(t => (double)t.Item2).ToArray();
                        }
                            break;
                        case PlotDataContentType.InventoryLevel: singleDatapoint = [double.Parse(elements[1], IOConstants.FORMATTER) * 100
                        ]; break;
                        case PlotDataContentType.SKUCountContained: singleDatapoint = [new InventoryLevelDatapoint(currentLine).ContainedSKUCount
                        ]; break;
                        case PlotDataContentType.InvCombinedTotal: singleDatapoint = [new InventoryLevelDatapoint(currentLine).InvCombinedTotal
                        ]; break;
                        case PlotDataContentType.InvCombinedRank: singleDatapoint = [new InventoryLevelDatapoint(currentLine).InvCombinedRank
                        ]; break;
                        case PlotDataContentType.InvCombinedAvgRank: singleDatapoint = [new InventoryLevelDatapoint(currentLine).InvCombinedAvgRank
                        ]; break;
                        case PlotDataContentType.InvSpeedTotal: singleDatapoint = [new InventoryLevelDatapoint(currentLine).InvSpeedTotal
                        ]; break;
                        case PlotDataContentType.InvSpeedRank: singleDatapoint = [new InventoryLevelDatapoint(currentLine).InvSpeedRank
                        ]; break;
                        case PlotDataContentType.InvSpeedAvgRank: singleDatapoint = [new InventoryLevelDatapoint(currentLine).InvSpeedAvgRank
                        ]; break;
                        case PlotDataContentType.InvUtilityTotal: singleDatapoint = [new InventoryLevelDatapoint(currentLine).InvUtilityTotal
                        ]; break;
                        case PlotDataContentType.InvUtilityRank: singleDatapoint = [new InventoryLevelDatapoint(currentLine).InvUtilityRank
                        ]; break;
                        case PlotDataContentType.InvUtilityAvgRank: singleDatapoint = [new InventoryLevelDatapoint(currentLine).InvUtilityAvgRank
                        ]; break;
                        case PlotDataContentType.MemoryUsage: singleDatapoint = [new PerformanceDatapoint(currentLine).MemoryUsage
                        ]; break;
                        case PlotDataContentType.RealTime: singleDatapoint = [new PerformanceDatapoint(currentLine).RealTime / 60.0
                        ]; break;
                        case PlotDataContentType.ControllerRuntimes:
                        {
                            singleDatapoint = new PerformanceDatapoint(currentLine).OverallControllerTimes.OrderBy(t => t.Item1).Select(t => t.Item2).ToArray();
                        }
                            break;
                        case PlotDataContentType.BundlesBacklogLevel: singleDatapoint = [int.Parse(elements[1])]; break;
                        case PlotDataContentType.OrdersBacklogLevel: singleDatapoint = [int.Parse(elements[2])]; break;
                        case PlotDataContentType.BundleThroughputAgeAverage: singleDatapoint = [double.Parse(elements[3], IOConstants.FORMATTER)
                        ]; break;
                        case PlotDataContentType.OrderThroughputAgeAverage: singleDatapoint = [double.Parse(elements[4], IOConstants.FORMATTER)
                        ]; break;
                        case PlotDataContentType.BundleTurnoverAgeAverage: singleDatapoint = [double.Parse(elements[5], IOConstants.FORMATTER)
                        ]; break;
                        case PlotDataContentType.OrderTurnoverAgeAverage: singleDatapoint = [double.Parse(elements[6], IOConstants.FORMATTER)
                        ]; break;
                        case PlotDataContentType.BundleFrequencyAverage: singleDatapoint = [double.Parse(elements[7], IOConstants.FORMATTER)
                        ]; break;
                        case PlotDataContentType.OrderFrequencyAverage: singleDatapoint = [double.Parse(elements[8], IOConstants.FORMATTER)
                        ]; break;
                        case PlotDataContentType.PodsHandledAtIS: singleDatapoint = [int.Parse(elements[1])]; break;
                        case PlotDataContentType.PodsHandledAtOS: singleDatapoint = [int.Parse(elements[2])]; break;
                        case PlotDataContentType.BundlePileon:
                        {
                            singleDatapoint = [new StationDataPoint(currentLine).IStationBundlesStored.Sum(s => s.Item2)
                            ];
                            singleSupportDatapoint1 = [new StationDataPoint(currentLine).PodsHandledAtIStations];
                        }
                            break;
                        case PlotDataContentType.ItemPileon:
                        {
                            singleDatapoint = [new StationDataPoint(currentLine).OStationItemsPicked.Sum(s => s.Item2)];
                            singleSupportDatapoint1 = [new StationDataPoint(currentLine).PodsHandledAtOStations];
                        }
                            break;
                        case PlotDataContentType.IStationIdleTime: { singleDatapoint = new StationDataPoint(currentLine).IStationIdleTimes.OrderBy(t => t.Item1).Select(t => t.Item2).ToArray(); } break;
                        case PlotDataContentType.OStationIdleTime: { singleDatapoint = new StationDataPoint(currentLine).OStationIdleTimes.OrderBy(t => t.Item1).Select(t => t.Item2).ToArray(); } break;
                        case PlotDataContentType.IStationActiveTime: { singleDatapoint = new StationDataPoint(currentLine).IStationActiveTimes.OrderBy(t => t.Item1).Select(t => t.Item2).ToArray(); } break;
                        case PlotDataContentType.OStationActiveTime: { singleDatapoint = new StationDataPoint(currentLine).OStationActiveTimes.OrderBy(t => t.Item1).Select(t => t.Item2).ToArray(); } break;
                        case PlotDataContentType.IStationRequests: { singleDatapoint = new StationDataPoint(currentLine).IStationRequests.OrderBy(t => t.Item1).Select(t => Convert.ToDouble(t.Item2)).ToArray(); } break;
                        case PlotDataContentType.OStationRequests: { singleDatapoint = new StationDataPoint(currentLine).OStationRequests.OrderBy(t => t.Item1).Select(t => Convert.ToDouble(t.Item2)).ToArray(); } break;
                        case PlotDataContentType.IStationOpenWork: { singleDatapoint = new StationDataPoint(currentLine).IStationBundleBacklog.OrderBy(t => t.Item1).Select(t => Convert.ToDouble(t.Item2)).ToArray(); } break;
                        case PlotDataContentType.OStationOpenWork: { singleDatapoint = new StationDataPoint(currentLine).OStationItemBacklog.OrderBy(t => t.Item1).Select(t => Convert.ToDouble(t.Item2)).ToArray(); } break;
                        case PlotDataContentType.IStationBots: { singleDatapoint = new StationDataPoint(currentLine).IStationAssignedBots.OrderBy(t => t.Item1).Select(t => Convert.ToDouble(t.Item2)).ToArray(); } break;
                        case PlotDataContentType.OStationBots: { singleDatapoint = new StationDataPoint(currentLine).OStationAssignedBots.OrderBy(t => t.Item1).Select(t => Convert.ToDouble(t.Item2)).ToArray(); } break;
                        case PlotDataContentType.IStationBundlesStored: { singleDatapoint = new StationDataPoint(currentLine).IStationBundlesStored.OrderBy(t => t.Item1).Select(t => Convert.ToDouble(t.Item2)).ToArray(); } break;
                        case PlotDataContentType.OStationItemsPicked: { singleDatapoint = new StationDataPoint(currentLine).OStationItemsPicked.OrderBy(t => t.Item1).Select(t => Convert.ToDouble(t.Item2)).ToArray(); } break;
                        case PlotDataContentType.IStationBundlePileon:
                        {
                            singleDatapoint = new StationDataPoint(currentLine).IStationBundlesStored.OrderBy(t => t.Item1).Select(t => Convert.ToDouble(t.Item2)).ToArray();
                            singleSupportDatapoint1 = new StationDataPoint(currentLine).IStationPodsHandled.OrderBy(t => t.Item1).Select(t => Convert.ToDouble(t.Item2)).ToArray();
                        }
                            break;
                        case PlotDataContentType.OStationItemPileon:
                        {
                            singleDatapoint = new StationDataPoint(currentLine).OStationItemsPicked.OrderBy(t => t.Item1).Select(t => Convert.ToDouble(t.Item2)).ToArray();
                            singleSupportDatapoint1 = new StationDataPoint(currentLine).OStationPodsHandled.OrderBy(t => t.Item1).Select(t => Convert.ToDouble(t.Item2)).ToArray();
                        }
                            break;
                        case PlotDataContentType.IStationsActive: singleDatapoint = [new StationDataPoint(currentLine).ActiveIStations
                        ]; break;
                        case PlotDataContentType.OStationsActive: singleDatapoint = [new StationDataPoint(currentLine).ActiveOStations
                        ]; break;
                        default: throw new ArgumentException("Unknown data type: " + dataType);
                    }
                    // Simply add the value to the current sector
                    datapoints.Add(new GenericProgressionDatapoint { Timestamp = timestamp, Values = singleDatapoint, SupportValues = singleSupportDatapoint1 });
                }
            }
            // Add dummy datapoints for count values to fill the gaps
            if (dataType == PlotDataContentType.OrdersHandled ||
                dataType == PlotDataContentType.BundlesHandled ||
                dataType == PlotDataContentType.ItemsHandled ||
                dataType == PlotDataContentType.OrdersPlaced ||
                dataType == PlotDataContentType.BundlesPlaced ||
                dataType == PlotDataContentType.LastMileTripTimeIStation ||
                dataType == PlotDataContentType.LastMileTripTimeOStation)
            {
                // Set maximal time length between two datapoints
                var maxGap = TimeSpan.FromMinutes(5).TotalSeconds;
                // Create first datapoint, if the gap at the start is too large
                if (maxGap < datapoints[0].Timestamp)
                    datapoints.Insert(0, new GenericProgressionDatapoint { Timestamp = 0, Values = [0] });
                // Create last datapoint, if the gap at the end is too large
                if (datapoints[datapoints.Count - 1].Timestamp < duration - maxGap)
                    datapoints.Add(new GenericProgressionDatapoint { Timestamp = duration, Values = [0] });
                // Search all consecutive tuples
                for (var i = 1; i < datapoints.Count; i++)
                {
                    // Check whether there is a gap that is too large
                    if (datapoints[i].Timestamp - datapoints[i - 1].Timestamp > maxGap)
                    {
                        // Fill the gap with dummy datapoints
                        var currentDummyTimestamp = datapoints[i - 1].Timestamp + maxGap;
                        while (datapoints[i].Timestamp - datapoints[i - 1].Timestamp > maxGap)
                        {
                            datapoints.Insert(i, new GenericProgressionDatapoint { Timestamp = currentDummyTimestamp, Values =
                                [0]
                            });
                            currentDummyTimestamp += maxGap;
                            i++;
                        }
                    }
                }
            }
            // Convert values
            var convertedDatapoints = new List<GenericProgressionDatapoint>();
            foreach (var datapoint in datapoints.ToArray())
            {
                var currentWindowStart = datapoint.Timestamp - 1800;
                var currentWindowEnd = datapoint.Timestamp + 1800;
                datapoints.RemoveRange(0, datapoints.TakeWhile(d => d.Timestamp < currentWindowStart).Count());
                var actualWindowStart = datapoints.Any() ? datapoints.First().Timestamp : currentWindowEnd;
                var currentWindowDatapoints = datapoints.TakeWhile(d => d.Timestamp <= currentWindowEnd);
                var actualWindowEnd = currentWindowDatapoints.Last().Timestamp;
                var windowCorrectionFactor = actualWindowEnd - actualWindowStart > 0 ? 1.0 / ((actualWindowEnd - actualWindowStart) / (currentWindowEnd - currentWindowStart)) : 1;
                switch (dataType)
                {
                    case PlotDataContentType.LastMileTripTimeOStation:
                    case PlotDataContentType.LastMileTripTimeIStation:
                    case PlotDataContentType.BotsQueueing:
                    case PlotDataContentType.InventoryLevel:
                    case PlotDataContentType.SKUCountContained:
                    case PlotDataContentType.InvCombinedTotal:
                    case PlotDataContentType.InvCombinedRank:
                    case PlotDataContentType.InvCombinedAvgRank:
                    case PlotDataContentType.InvSpeedTotal:
                    case PlotDataContentType.InvSpeedRank:
                    case PlotDataContentType.InvSpeedAvgRank:
                    case PlotDataContentType.InvUtilityTotal:
                    case PlotDataContentType.InvUtilityRank:
                    case PlotDataContentType.InvUtilityAvgRank:
                    case PlotDataContentType.BundlesBacklogLevel:
                    case PlotDataContentType.OrdersBacklogLevel:
                    case PlotDataContentType.BundleThroughputTimeAvg:
                    case PlotDataContentType.BundleTurnoverTimeAvg:
                    case PlotDataContentType.OrderThroughputTimeAvg:
                    case PlotDataContentType.OrderTurnoverTimeAvg:
                    case PlotDataContentType.BundleThroughputAgeAverage:
                    case PlotDataContentType.OrderThroughputAgeAverage:
                    case PlotDataContentType.BundleTurnoverAgeAverage:
                    case PlotDataContentType.OrderTurnoverAgeAverage:
                    case PlotDataContentType.BundleFrequencyAverage:
                    case PlotDataContentType.OrderFrequencyAverage:
                    case PlotDataContentType.IStationsActive:
                    case PlotDataContentType.OStationsActive:
                    case PlotDataContentType.MemoryUsage:
                    case PlotDataContentType.RealTime:
                        convertedDatapoints.Add(new GenericProgressionDatapoint
                        {
                            Timestamp = datapoint.Timestamp,
                            Values = [datapoint.Values[0]]
                        });
                        break;
                    case PlotDataContentType.OrdersHandled:
                    case PlotDataContentType.BundlesHandled:
                    case PlotDataContentType.ItemsHandled:
                    case PlotDataContentType.OrdersPlaced:
                    case PlotDataContentType.BundlesPlaced:
                    case PlotDataContentType.DistanceTraveled:
                        convertedDatapoints.Add(new GenericProgressionDatapoint
                        {
                            Timestamp = datapoint.Timestamp,
                            Values = [currentWindowDatapoints.Sum(d => d.Values[0]) * windowCorrectionFactor]
                        });
                        break;
                    case PlotDataContentType.OrderLatenessAvg:
                        convertedDatapoints.Add(new GenericProgressionDatapoint
                        {
                            Timestamp = datapoint.Timestamp,
                            Values = [currentWindowDatapoints.Any(s => s.Values[0] > 0) ? currentWindowDatapoints.Where(s => s.Values[0] > 0).Average(s => s.Values[0]) : 0
                            ]
                        });
                        break;
                    case PlotDataContentType.LateOrderFractional:
                        convertedDatapoints.Add(new GenericProgressionDatapoint
                        {
                            Timestamp = datapoint.Timestamp,
                            Values = [(double)currentWindowDatapoints.Count(s => s.Values[0] > 0) / currentWindowDatapoints.Count() * 100
                            ]
                        });
                        break;
                    case PlotDataContentType.ControllerRuntimes:
                    {
                        var convertedPoint = new GenericProgressionDatapoint
                        {
                            Timestamp = datapoint.Timestamp,
                            Values = new double[datapoint.Values.Length],
                        };
                        convertedDatapoints.Add(convertedPoint);
                        for (var i = 0; i < datapoint.Values.Length; i++)
                            convertedPoint.Values[i] = (currentWindowDatapoints.Last().Values[i] - currentWindowDatapoints.First().Values[i]) * windowCorrectionFactor;
                    }
                        break;
                    case PlotDataContentType.PodsHandledAtIS:
                    case PlotDataContentType.PodsHandledAtOS:
                        convertedDatapoints.Add(new GenericProgressionDatapoint
                        {
                            Timestamp = datapoint.Timestamp,
                            Values = [(currentWindowDatapoints.Last().Values[0] - currentWindowDatapoints.First().Values[0]) * windowCorrectionFactor
                            ]
                        });
                        break;
                    case PlotDataContentType.BundlePileon:
                    case PlotDataContentType.ItemPileon:
                        convertedDatapoints.Add(new GenericProgressionDatapoint
                        {
                            Timestamp = datapoint.Timestamp,
                            Values =
                            [
                                currentWindowDatapoints.Last().SupportValues[0] - currentWindowDatapoints.First().SupportValues[0] > 0 ?
                                (currentWindowDatapoints.Last().Values[0] - currentWindowDatapoints.First().Values[0]) /
                                (currentWindowDatapoints.Last().SupportValues[0] - currentWindowDatapoints.First().SupportValues[0]) :
                                0
                            ]
                        });
                        break;
                    case PlotDataContentType.IStationIdleTime:
                    case PlotDataContentType.OStationIdleTime:
                    case PlotDataContentType.IStationActiveTime:
                    case PlotDataContentType.OStationActiveTime:
                    {
                        var convertedPoint = new GenericProgressionDatapoint
                        {
                            Timestamp = datapoint.Timestamp,
                            Values = new double[datapoint.Values.Length],
                        };
                        convertedDatapoints.Add(convertedPoint);
                        for (var i = 0; i < datapoint.Values.Length; i++)
                            convertedPoint.Values[i] =
                                (currentWindowDatapoints.Last().Values[i] - currentWindowDatapoints.First().Values[i]) /
                                (currentWindowDatapoints.Last().Timestamp - currentWindowDatapoints.First().Timestamp) * 100;
                    }
                        break;
                    case PlotDataContentType.IStationRequests:
                    case PlotDataContentType.OStationRequests:
                    case PlotDataContentType.IStationOpenWork:
                    case PlotDataContentType.OStationOpenWork:
                    case PlotDataContentType.IStationBots:
                    case PlotDataContentType.OStationBots:
                    case PlotDataContentType.TaskBotCounts:
                    {
                        var convertedPoint = new GenericProgressionDatapoint
                        {
                            Timestamp = datapoint.Timestamp,
                            Values = new double[datapoint.Values.Length],
                        };
                        convertedDatapoints.Add(convertedPoint);
                        for (var i = 0; i < datapoint.Values.Length; i++)
                            convertedPoint.Values[i] = currentWindowDatapoints.Average(d => d.Values[i]);
                    }
                        break;
                    case PlotDataContentType.IStationBundlesStored:
                    case PlotDataContentType.OStationItemsPicked:
                    {
                        var convertedPoint = new GenericProgressionDatapoint
                        {
                            Timestamp = datapoint.Timestamp,
                            Values = new double[datapoint.Values.Length],
                        };
                        convertedDatapoints.Add(convertedPoint);
                        for (var i = 0; i < datapoint.Values.Length; i++)
                            convertedPoint.Values[i] = (currentWindowDatapoints.Last().Values[i] - currentWindowDatapoints.First().Values[i]) * windowCorrectionFactor;
                    }
                        break;
                    case PlotDataContentType.IStationBundlePileon:
                    case PlotDataContentType.OStationItemPileon:
                    {
                        var convertedPoint = new GenericProgressionDatapoint
                        {
                            Timestamp = datapoint.Timestamp,
                            Values = new double[datapoint.Values.Length],
                        };
                        convertedDatapoints.Add(convertedPoint);
                        for (var i = 0; i < datapoint.Values.Length; i++)
                            convertedPoint.Values[i] =
                                currentWindowDatapoints.Last().SupportValues[i] - currentWindowDatapoints.First().SupportValues[i] > 0 ?
                                    (currentWindowDatapoints.Last().Values[i] - currentWindowDatapoints.First().Values[i]) /
                                    (currentWindowDatapoints.Last().SupportValues[i] - currentWindowDatapoints.First().SupportValues[i]) :
                                    0;
                    }
                        break;
                    default: throw new ArgumentException("Unknown data type: " + dataType);
                }
            }
            datapoints = null;
            // Apply moving average
            if (customDiagram.AverageWindow > 0)
            {
                var averagedDatapoints = new List<GenericProgressionDatapoint>();
                foreach (var datapoint in convertedDatapoints.ToArray())
                {
                    var currentWindowStart = datapoint.Timestamp - 1800;
                    var currentWindowEnd = datapoint.Timestamp + 1800;
                    convertedDatapoints.RemoveRange(0, convertedDatapoints.TakeWhile(d => d.Timestamp < currentWindowStart).Count());
                    var currentWindowDatapoints = convertedDatapoints.TakeWhile(d => d.Timestamp <= currentWindowEnd);
                    var averagedDatapoint = new GenericProgressionDatapoint
                    {
                        Timestamp = datapoint.Timestamp,
                        Values = new double[datapoint.Values.Length],
                        SupportValues = datapoint.SupportValues != null ? new double[datapoint.SupportValues.Length] : null,
                    };
                    averagedDatapoints.Add(averagedDatapoint);
                    for (var i = 0; i < datapoint.Values.Length; i++)
                        averagedDatapoint.Values[i] = currentWindowDatapoints.Average(d => d.Values[i]);
                    if (datapoint.SupportValues != null)
                        for (var i = 0; i < datapoint.SupportValues.Length; i++)
                            averagedDatapoint.SupportValues[i] = currentWindowDatapoints.Average(d => d.SupportValues[i]);
                }
                convertedDatapoints = averagedDatapoints;
            }
            // Convert time info
            foreach (var datapoint in convertedDatapoints)
            {
                datapoint.Timestamp = customDiagram.PlotTime switch
                {
                    PlotTime.Minute => TimeSpan.FromSeconds(datapoint.Timestamp).TotalMinutes,
                    PlotTime.Hour => TimeSpan.FromSeconds(datapoint.Timestamp).TotalHours,
                    PlotTime.Day => TimeSpan.FromSeconds(datapoint.Timestamp).TotalDays,
                    _ => throw new ArgumentException("Unknown time: " + customDiagram.PlotTime)
                };
            }

            // Write output file
            using (var sw = new StreamWriter(outputFile))
            {
                // Write header
                switch (dataType)
                {
                    case PlotDataContentType.IStationIdleTime:
                    {
                        // Generate a header reflecting the stations
                        var sampleDatapoint = new StationDataPoint(sampleLine);
                        sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " SectorTime " +
                                     string.Join(IOConstants.GNU_PLOT_VALUE_SPLIT.ToString(), sampleDatapoint.IStationIdleTimes.OrderBy(t => t.Item1).Select(t => "station" + t.Item1)));
                    }
                        break;
                    case PlotDataContentType.OStationIdleTime:
                    {
                        // Generate a header reflecting the stations
                        var sampleDatapoint = new StationDataPoint(sampleLine);
                        sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " SectorTime " +
                                     string.Join(IOConstants.GNU_PLOT_VALUE_SPLIT.ToString(), sampleDatapoint.OStationIdleTimes.OrderBy(t => t.Item1).Select(t => "station" + t.Item1)));
                    }
                        break;
                    case PlotDataContentType.IStationActiveTime:
                    {
                        // Generate a header reflecting the stations
                        var sampleDatapoint = new StationDataPoint(sampleLine);
                        sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " SectorTime " +
                                     string.Join(IOConstants.GNU_PLOT_VALUE_SPLIT.ToString(), sampleDatapoint.IStationActiveTimes.OrderBy(t => t.Item1).Select(t => "station" + t.Item1)));
                    }
                        break;
                    case PlotDataContentType.OStationActiveTime:
                    {
                        // Generate a header reflecting the stations
                        var sampleDatapoint = new StationDataPoint(sampleLine);
                        sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " SectorTime " +
                                     string.Join(IOConstants.GNU_PLOT_VALUE_SPLIT.ToString(), sampleDatapoint.OStationActiveTimes.OrderBy(t => t.Item1).Select(t => "station" + t.Item1)));
                    }
                        break;
                    case PlotDataContentType.IStationRequests:
                    {
                        // Generate a header reflecting the stations
                        var sampleDatapoint = new StationDataPoint(sampleLine);
                        sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " SectorTime " +
                                     string.Join(IOConstants.GNU_PLOT_VALUE_SPLIT.ToString(), sampleDatapoint.IStationRequests.OrderBy(t => t.Item1).Select(t => "station" + t.Item1)));
                    }
                        break;
                    case PlotDataContentType.OStationRequests:
                    {
                        // Generate a header reflecting the stations
                        var sampleDatapoint = new StationDataPoint(sampleLine);
                        sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " SectorTime " +
                                     string.Join(IOConstants.GNU_PLOT_VALUE_SPLIT.ToString(), sampleDatapoint.OStationRequests.OrderBy(t => t.Item1).Select(t => "station" + t.Item1)));
                    }
                        break;
                    case PlotDataContentType.IStationOpenWork:
                    {
                        // Generate a header reflecting the stations
                        var sampleDatapoint = new StationDataPoint(sampleLine);
                        sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " SectorTime " +
                                     string.Join(IOConstants.GNU_PLOT_VALUE_SPLIT.ToString(), sampleDatapoint.IStationBundleBacklog.OrderBy(t => t.Item1).Select(t => "station" + t.Item1)));
                    }
                        break;
                    case PlotDataContentType.OStationOpenWork:
                    {
                        // Generate a header reflecting the stations
                        var sampleDatapoint = new StationDataPoint(sampleLine);
                        sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " SectorTime " +
                                     string.Join(IOConstants.GNU_PLOT_VALUE_SPLIT.ToString(), sampleDatapoint.OStationItemBacklog.OrderBy(t => t.Item1).Select(t => "station" + t.Item1)));
                    }
                        break;
                    case PlotDataContentType.IStationBots:
                    {
                        // Generate a header reflecting the stations
                        var sampleDatapoint = new StationDataPoint(sampleLine);
                        sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " SectorTime " +
                                     string.Join(IOConstants.GNU_PLOT_VALUE_SPLIT.ToString(), sampleDatapoint.IStationAssignedBots.OrderBy(t => t.Item1).Select(t => "station" + t.Item1)));
                    }
                        break;
                    case PlotDataContentType.OStationBots:
                    {
                        // Generate a header reflecting the stations
                        var sampleDatapoint = new StationDataPoint(sampleLine);
                        sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " SectorTime " +
                                     string.Join(IOConstants.GNU_PLOT_VALUE_SPLIT.ToString(), sampleDatapoint.OStationAssignedBots.OrderBy(t => t.Item1).Select(t => "station" + t.Item1)));
                    }
                        break;
                    case PlotDataContentType.IStationBundlesStored:
                    {
                        // Generate a header reflecting the stations
                        var sampleDatapoint = new StationDataPoint(sampleLine);
                        sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " SectorTime " +
                                     string.Join(IOConstants.GNU_PLOT_VALUE_SPLIT.ToString(), sampleDatapoint.IStationBundlesStored.OrderBy(t => t.Item1).Select(t => "station" + t.Item1)));
                    }
                        break;
                    case PlotDataContentType.OStationItemsPicked:
                    {
                        // Generate a header reflecting the stations
                        var sampleDatapoint = new StationDataPoint(sampleLine);
                        sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " SectorTime " +
                                     string.Join(IOConstants.GNU_PLOT_VALUE_SPLIT.ToString(), sampleDatapoint.OStationItemsPicked.OrderBy(t => t.Item1).Select(t => "station" + t.Item1)));
                    }
                        break;
                    case PlotDataContentType.IStationBundlePileon:
                    {
                        // Generate a header reflecting the stations
                        var sampleDatapoint = new StationDataPoint(sampleLine);
                        sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " SectorTime " +
                                     string.Join(IOConstants.GNU_PLOT_VALUE_SPLIT.ToString(), sampleDatapoint.IStationBundlesStored.OrderBy(t => t.Item1).Select(t => "station" + t.Item1)));
                    }
                        break;
                    case PlotDataContentType.OStationItemPileon:
                    {
                        // Generate a header reflecting the stations
                        var sampleDatapoint = new StationDataPoint(sampleLine);
                        sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " SectorTime " +
                                     string.Join(IOConstants.GNU_PLOT_VALUE_SPLIT.ToString(), sampleDatapoint.OStationItemsPicked.OrderBy(t => t.Item1).Select(t => "station" + t.Item1)));
                    }
                        break;
                    case PlotDataContentType.ControllerRuntimes:
                    {
                        // Generate a header reflecting the stations
                        var sampleDatapoint = new PerformanceDatapoint(sampleLine);
                        sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " SectorTime " +
                                     string.Join(IOConstants.GNU_PLOT_VALUE_SPLIT.ToString(), sampleDatapoint.OverallControllerTimes.OrderBy(t => t.Item1).Select(t => t.Item1)));
                    }
                        break;
                    case PlotDataContentType.TaskBotCounts:
                    {
                        // Generate a header reflecting the stations
                        var sampleDatapoint = new BotDatapoint(sampleLine);
                        sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " SectorTime " +
                                     string.Join(IOConstants.GNU_PLOT_VALUE_SPLIT.ToString(), sampleDatapoint.TaskBotCounts.OrderBy(t => t.Item1).Select(t => t.Item1)));
                    }
                        break;
                    case PlotDataContentType.OrdersHandled:
                    case PlotDataContentType.BundlesHandled:
                    case PlotDataContentType.ItemsHandled:
                    case PlotDataContentType.OrdersPlaced:
                    case PlotDataContentType.BundlesPlaced:
                    case PlotDataContentType.OrderThroughputTimeAvg:
                    case PlotDataContentType.OrderTurnoverTimeAvg:
                    case PlotDataContentType.OrderLatenessAvg:
                    case PlotDataContentType.LateOrderFractional:
                    case PlotDataContentType.BundleThroughputTimeAvg:
                    case PlotDataContentType.BundleTurnoverTimeAvg:
                    case PlotDataContentType.OrdersBacklogLevel:
                    case PlotDataContentType.BundlesBacklogLevel:
                    case PlotDataContentType.OrderThroughputAgeAverage:
                    case PlotDataContentType.BundleThroughputAgeAverage:
                    case PlotDataContentType.OrderTurnoverAgeAverage:
                    case PlotDataContentType.BundleTurnoverAgeAverage:
                    case PlotDataContentType.OrderFrequencyAverage:
                    case PlotDataContentType.BundleFrequencyAverage:
                    case PlotDataContentType.PodsHandledAtIS:
                    case PlotDataContentType.PodsHandledAtOS:
                    case PlotDataContentType.BundlePileon:
                    case PlotDataContentType.ItemPileon:
                    case PlotDataContentType.IStationsActive:
                    case PlotDataContentType.OStationsActive:
                    case PlotDataContentType.DistanceTraveled:
                    case PlotDataContentType.LastMileTripTimeOStation:
                    case PlotDataContentType.LastMileTripTimeIStation:
                    case PlotDataContentType.BotsQueueing:
                    case PlotDataContentType.InventoryLevel:
                    case PlotDataContentType.SKUCountContained:
                    case PlotDataContentType.InvCombinedTotal:
                    case PlotDataContentType.InvCombinedRank:
                    case PlotDataContentType.InvCombinedAvgRank:
                    case PlotDataContentType.InvSpeedTotal:
                    case PlotDataContentType.InvSpeedRank:
                    case PlotDataContentType.InvSpeedAvgRank:
                    case PlotDataContentType.InvUtilityTotal:
                    case PlotDataContentType.InvUtilityRank:
                    case PlotDataContentType.InvUtilityAvgRank:
                    case PlotDataContentType.MemoryUsage:
                    case PlotDataContentType.RealTime:
                        sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " SectorTime " + dataType);
                        break;
                    default: throw new ArgumentException("Unknown data type: " + dataType);
                }

                // Write data points
                foreach (var datapoint in convertedDatapoints)
                {
                    sw.WriteLine(
                        // Plot the time for this sector - use the middle of the sector as the reference instead of the end
                        datapoint.Timestamp.ToString(IOConstants.FORMATTER) + IOConstants.GNU_PLOT_VALUE_SPLIT +
                        // Plot the value measured for the sector
                        string.Join(IOConstants.GNU_PLOT_VALUE_SPLIT.ToString(), datapoint.Values.Select(v => v.ToString(IOConstants.FORMATTER))));
                }
            }
        }
    }

    private void PrepareFixedTimeProgression(string outputDirPath, PreparationConfiguration config, double duration)
    {
        foreach (var customDiagram in config.CustomDiagrams)
        {
            //foreach (var dataType in customDiagram.Plots.SelectMany(c => c.Select(p => p.DataType)).Distinct())
            // Generate intermediate files in parallel
            Parallel.ForEach(
                // For all required dat files
                customDiagram.Plots.SelectMany(c => c.Select(p => p.DataType)).Distinct(),
                // Parallelize up to given limit
                new ParallelOptions { MaxDegreeOfParallelism = config.DegreeOfParallelism },
                // Generate file for given data type
                (PlotDataContentType dataType) =>
                {
                    PrepareSingleFixedTimeProgression(outputDirPath, customDiagram, duration, dataType);
                });
        }
    }

    #endregion

    #region Helper methods

    #region Gnuplot parameters

    private static string GenerateOutputDefinitionScriptCode(string plotWidth, string plotHeight)
    {
        var preamble = "reset" + IOConstants.LINE_TERMINATOR +
                       "# Output definition" + IOConstants.LINE_TERMINATOR +
                       "set terminal pdfcairo enhanced" + (plotWidth != null && plotHeight != null ? " size " + plotWidth + ", " + plotHeight : "") + " font \"Consolas, 12\"" + IOConstants.LINE_TERMINATOR +
                       "set lmargin 13" + IOConstants.LINE_TERMINATOR +
                       "set rmargin 13";
        return preamble;
    }

    private static string GenerateOutputFileScriptCode(string filename)
    {
        return "set output \"" + filename + "\"";
    }

    private static string GenerateInstanceBasedDiagramTitle(string instance, string setting, string config)
    {
        return instance + " / " + setting + " / " + config;
    }

    private static string GenerateParameterScriptCode(QuadDirections keyPosition, string xLabel, string yLabel, bool logX, bool logY, double xMin, double xMax, double yMin, double yMax)
    {
        var keyPositionParam = "right bottom Right";
        switch (keyPosition)
        {
            case QuadDirections.NW:
                keyPositionParam = "left top Left";
                break;
            case QuadDirections.NE:
                keyPositionParam = "right top Right";
                break;
            case QuadDirections.SW:
                keyPositionParam = "left bottom Left";
                break;
            case QuadDirections.SE: // Do nothing - default
            default:
                break;
        }

        return "# Parameters" + IOConstants.LINE_TERMINATOR +
               "set key " + keyPositionParam + IOConstants.LINE_TERMINATOR +
               (xLabel != null ? "set xlabel \"" + xLabel + "\"" : "unset xlabel") + IOConstants.LINE_TERMINATOR +
               (yLabel != null ? "set ylabel \"" + yLabel + "\"" : "unset ylabel") + IOConstants.LINE_TERMINATOR +
               (logX ? "set log x" + IOConstants.LINE_TERMINATOR : "") +
               (logY ? "set log y" + IOConstants.LINE_TERMINATOR : "") +
               (double.IsNaN(xMin) || double.IsNaN(xMax) ? "" : "set xrange [" + xMin + ":" + xMax + "]" + IOConstants.LINE_TERMINATOR) +
               (double.IsNaN(yMin) || double.IsNaN(yMax) ? "" : "set yrange [" + yMin + ":" + yMax + "]" + IOConstants.LINE_TERMINATOR) +
               "set grid" + IOConstants.LINE_TERMINATOR +
               "set style fill solid 0.25";
    }

    internal static string GenerateLineStyleScriptCode()
    {
        // The colors used in here match with those defined in the LaTeX documents of the thesis (13.04.16)
        var lineIndex = 0;
        return "# Line-Styles" + IOConstants.LINE_TERMINATOR +
               string.Join(IOConstants.LINE_TERMINATOR, PlotColoring.OrderedHexCodes.Select(hex => "set style line " + ++lineIndex + " linetype 1 linecolor rgb \"" + hex + "\" linewidth 3"));
    }

    internal static string GenerateLineStyleScriptCodeWithPoints()
    {
        // The colors used in here match with those defined in the LaTeX documents of the thesis (13.04.16)
        var lineIndex = 0;
        return "# Line-Styles" + IOConstants.LINE_TERMINATOR +
               string.Join(IOConstants.LINE_TERMINATOR, PlotColoring.OrderedHexCodes.Select(hex => "set style line " + ++lineIndex + " linetype 1 linecolor rgb \"" + hex + "\" linewidth 1 pt " + lineIndex));
    }

    private static string GenerateTailScriptCode()
    {
        return "reset" + IOConstants.LINE_TERMINATOR + "exit";
    }

    #endregion

    #region Gnuplot plots

    private string GenerateProgressionScript(
        PreparationConfiguration config,
        string outputDirPath,
        string instanceName,
        string configName,
        bool plotItemProgression,
        bool plotBundleProgression,
        bool plotOrderProgression,
        bool plotCollisionProgression,
        bool plotDistanceProgression,
        bool plotOrderCollisionProgressionCombined,
        bool plotOrderDistanceProgressionCombined,
        bool plotItemCollisionProgressionCombined,
        bool plotOrderBundleDistanceProgressionCombined)
    {
        // Generate plot script
        using (var sw = new StreamWriter(Path.Combine(outputDirPath, IOConstants.STAT_PROGRESSION_SCRIPT_FILENAME + ".gp")))
        {
            // Preamble
            sw.WriteLine(GenerateOutputDefinitionScriptCode(null, null));
            sw.WriteLine(GenerateParameterScriptCode(QuadDirections.NW, "Time (in s)", "Throughput", false, false, double.NaN, double.NaN, double.NaN, double.NaN));
            sw.WriteLine(GenerateLineStyleScriptCode());
            if (config.PrintDiagramTitle)
                sw.WriteLine("set title \"" + instanceName + (string.IsNullOrWhiteSpace(configName) ? "" : " / " + configName) + "\"");
            // Plots
            if (plotItemProgression)
            {
                sw.WriteLine("set ylabel \"Item throughput (items/min)\"");
                sw.WriteLine(GenerateOutputFileScriptCode("itemprogression.pdf"));
                sw.WriteLine("plot \\");
                sw.WriteLine("\"" + IOConstants.STAT_ITEM_PROGRESSION_RESULT_FILENAME + ".dat" + "\" u 1:2 smooth bezier linestyle 1 t \"Item throughput\"");
            }
            if (plotBundleProgression)
            {
                sw.WriteLine("set ylabel \"Bundle throughput (bundles/min)\"");
                sw.WriteLine(GenerateOutputFileScriptCode("bundleprogression.pdf"));
                sw.WriteLine("plot \\");
                sw.WriteLine("\"" + IOConstants.STAT_BUNDLE_PROGRESSION_RESULT_FILENAME + ".dat" + "\" u 1:2 smooth bezier linestyle 1 t \"Bundle throughput\"");
            }
            if (plotOrderProgression)
            {
                sw.WriteLine("set ylabel \"Order throughput (orders/min)\"");
                sw.WriteLine(GenerateOutputFileScriptCode("orderprogression.pdf"));
                sw.WriteLine("plot \\");
                sw.WriteLine("\"" + IOConstants.STAT_ORDER_PROGRESSION_RESULT_FILENAME + ".dat" + "\" u 1:2 smooth bezier linestyle 1 t \"Order throughput\"");
            }
            if (plotCollisionProgression)
            {
                sw.WriteLine("set ylabel \"Collisions (reports/min)\"");
                sw.WriteLine(GenerateOutputFileScriptCode("collisionprogression.pdf"));
                sw.WriteLine("plot \\");
                sw.WriteLine("\"" + IOConstants.STAT_COLLISION_PROGRESSION_RESULT_FILENAME + ".dat" + "\" u 1:2 smooth bezier linestyle 1 t \"Collisions\"");
            }
            if (plotDistanceProgression)
            {
                sw.WriteLine("set ylabel \"Distance (m/min)\"");
                sw.WriteLine(GenerateOutputFileScriptCode("distanceprogression.pdf"));
                sw.WriteLine("plot \\");
                sw.WriteLine("\"" + IOConstants.STAT_TRAVELED_DISTANCE_PROGRESSION_RESULT_FILENAME + ".dat" + "\" u 1:2 smooth bezier linestyle 1 t \"Distance traveled\"");
            }
            if (plotOrderCollisionProgressionCombined)
            {
                sw.WriteLine("set y2label \"Collisions (reports/min)\"");
                sw.WriteLine("set ylabel \"Order throughput (orders/min)\"");
                sw.WriteLine(GenerateOutputFileScriptCode("collisionsorderscombined.pdf"));
                sw.WriteLine("set y2tics");
                sw.WriteLine("plot \\");
                sw.WriteLine("\"" + IOConstants.STAT_COLLISION_PROGRESSION_RESULT_FILENAME + ".dat\" using 1:2 smooth bezier axes x1y2 with lines linestyle 1 t \"Collisions\", \\");
                sw.WriteLine("\"" + IOConstants.STAT_ORDER_PROGRESSION_RESULT_FILENAME + ".dat\" using 1:2 smooth bezier axes x1y1 with lines linestyle 2 t \"Order throughput\"");
            }
            if (plotOrderDistanceProgressionCombined)
            {
                sw.WriteLine("set y2label \"Distance (m/min)\"");
                sw.WriteLine("set ylabel \"Order throughput (orders/min)\"");
                sw.WriteLine(GenerateOutputFileScriptCode("distanceorderscombined.pdf"));
                sw.WriteLine("set y2tics");
                sw.WriteLine("plot \\");
                sw.WriteLine("\"" + IOConstants.STAT_TRAVELED_DISTANCE_PROGRESSION_RESULT_FILENAME + ".dat\" using 1:2 smooth bezier axes x1y2 with lines linestyle 1 t \"Distance traveled\", \\");
                sw.WriteLine("\"" + IOConstants.STAT_ORDER_PROGRESSION_RESULT_FILENAME + ".dat\" using 1:2 smooth bezier axes x1y1 with lines linestyle 2 t \"Order throughput\"");
            }
            if (plotItemCollisionProgressionCombined)
            {
                sw.WriteLine("set y2label \"Collisions (reports/min)\"");
                sw.WriteLine("set ylabel \"Item throughput (items/min)\"");
                sw.WriteLine(GenerateOutputFileScriptCode("collisionsitemscombined.pdf"));
                sw.WriteLine("set y2tics");
                sw.WriteLine("plot \\");
                sw.WriteLine("\"" + IOConstants.STAT_COLLISION_PROGRESSION_RESULT_FILENAME + ".dat\" using 1:2 smooth bezier axes x1y2 with lines linestyle 1 t \"Collisions\", \\");
                sw.WriteLine("\"" + IOConstants.STAT_ITEM_PROGRESSION_RESULT_FILENAME + ".dat\" using 1:2 smooth bezier axes x1y1 with lines linestyle 2 t \"Item throughput\"");
            }
            if (plotOrderBundleDistanceProgressionCombined)
            {
                sw.WriteLine("set y2label \"Throughput ([bundles,orders]/min)\"");
                sw.WriteLine("set ylabel \"Distance (m/min)\"");
                sw.WriteLine(GenerateOutputFileScriptCode("bundlesordersdistancecombined.pdf"));
                sw.WriteLine("set y2tics");
                sw.WriteLine("plot \\");
                sw.WriteLine("\"" + IOConstants.STAT_BUNDLE_PROGRESSION_RESULT_FILENAME + ".dat\" using 1:2 smooth bezier axes x1y2 with lines linestyle 1 t \"Bundle throughput\", \\");
                sw.WriteLine("\"" + IOConstants.STAT_ORDER_PROGRESSION_RESULT_FILENAME + ".dat\" using 1:2 smooth bezier axes x1y2 with lines linestyle 2 t \"Order throughput\", \\");
                sw.WriteLine("\"" + IOConstants.STAT_TRAVELED_DISTANCE_PROGRESSION_RESULT_FILENAME + ".dat\" using 1:2 smooth bezier axes x1y1 with lines linestyle 3 t \"Distance traveled\"");
            }
            // Tail
            sw.WriteLine(GenerateTailScriptCode());
        }
        // Generate short batch script
        string scriptPath;
        if (PlatformHelpers.IsLinux)
        {
            scriptPath = Path.Combine(outputDirPath, IOConstants.STAT_PROGRESSION_SCRIPT_FILENAME + ".sh");
            using (var sw = new StreamWriter(scriptPath))
            {
                sw.WriteLine("#!/bin/bash");
                sw.WriteLine("gnuplot " + IOConstants.STAT_PROGRESSION_SCRIPT_FILENAME + ".gp");
            }
            if (PlatformHelpers.IsLinux)
                PlatformHelpers.SetExecutableAttribute(scriptPath, Config.Logger);
        }
        else
        {
            scriptPath = Path.Combine(outputDirPath, IOConstants.STAT_PROGRESSION_SCRIPT_FILENAME + ".cmd");
            using (var sw = new StreamWriter(scriptPath))
            {
                sw.WriteLine("gnuplot " + IOConstants.STAT_PROGRESSION_SCRIPT_FILENAME + ".gp");
            }
        }
        return scriptPath;
    }

    public IEnumerable<string> GenerateCustomScript(string outputDirPath, string instanceName, string settingName, string configName, List<CustomDiagramConfiguration> diagrams, double duration)
    {
        var scriptPaths = new List<string>();
        foreach (var customDiagram in diagrams)
        {
            // Generate plot script
            using (var sw = new StreamWriter(Path.Combine(outputDirPath, customDiagram.Name + ".gp")))
            {
                // Preamble
                var timeUnit = customDiagram.PlotTime == PlotTime.Day ? "days" : customDiagram.PlotTime == PlotTime.Hour ? "hours" : "min";
                sw.WriteLine(GenerateOutputDefinitionScriptCode(7.ToString(IOConstants.FORMATTER), (3 * customDiagram.Plots.Count).ToString(IOConstants.FORMATTER)));
                sw.WriteLine(GenerateOutputFileScriptCode(customDiagram.Name + ".pdf"));
                sw.WriteLine(GenerateLineStyleScriptCode());
                // Set xrange
                duration = customDiagram.PlotTime switch
                {
                    PlotTime.Minute => TimeSpan.FromSeconds(duration).TotalMinutes,
                    PlotTime.Hour => TimeSpan.FromSeconds(duration).TotalHours,
                    PlotTime.Day => TimeSpan.FromSeconds(duration).TotalDays,
                    _ => throw new ArgumentException("Unknown time: " + customDiagram.PlotTime)
                };
                sw.WriteLine("set xrange [" + 0.ToString(IOConstants.FORMATTER) + ":" + duration.ToString(IOConstants.FORMATTER) + "]");
                // If more than one plot, activate multiplot
                if (customDiagram.Plots.Count > 1)
                    sw.WriteLine("set multiplot layout " + customDiagram.Plots.Count + ", 1 title \"" + GenerateInstanceBasedDiagramTitle(instanceName, settingName, configName) + "\"");
                // Output all plots
                foreach (var diagram in customDiagram.Plots)
                {
                    sw.WriteLine(GenerateParameterScriptCode(
                        QuadDirections.NW,
                        diagram == customDiagram.Plots.Last() ? "Time (in " + timeUnit + ")" : null,
                        "Throughput",
                        false,
                        false,
                        double.NaN,
                        double.NaN,
                        double.NaN,
                        double.NaN));
                    if (customDiagram.PrintDiagramTitle && customDiagram.Plots.Count <= 1)
                        sw.WriteLine("set title \"" + GenerateInstanceBasedDiagramTitle(instanceName, settingName, configName) + "\"");
                    // Set axis parameters
                    var y1set = false; var y2set = false;
                    foreach (var plot in diagram)
                    {
                        switch (plot.Axis)
                        {
                            case PlotAxis.Y1: { if (!y1set) { y1set = true; sw.WriteLine("set ylabel \"" + plot.GetUnitDescription() + "\""); } } break;
                            case PlotAxis.Y2: { if (!y2set) { y2set = true; sw.WriteLine("set y2label \"" + plot.GetUnitDescription() + "\""); sw.WriteLine("set y2tics"); } } break;
                            default: throw new ArgumentException("Unknown axis: " + plot.Axis);
                        }
                    }
                    // Write plots
                    var singlePlotCounter = 0;
                    sw.WriteLine("plot \\");
                    for (var dataPlotNumber = 0; dataPlotNumber < diagram.Count; dataPlotNumber++)
                    {
                        // See whether there is more than one column to plot
                        string[] headerElements;
                        using (var sr = new StreamReader(Path.Combine(outputDirPath, IntermediateFileNamer(diagram[dataPlotNumber].DataType, customDiagram.PlotTime, customDiagram.AverageWindow))))
                            headerElements = sr.ReadLine().Split(IOConstants.GNU_PLOT_VALUE_SPLIT);
                        // Plot every column of the dat file
                        for (var colIndex = 2; colIndex < headerElements.Length; colIndex++)
                            // If there is more than one column, add the header of the dat file column to the title
                            sw.WriteLine("\"" + IntermediateFileNamer(diagram[dataPlotNumber].DataType, customDiagram.PlotTime, customDiagram.AverageWindow) + "\" using 1:" + colIndex + " " +
                                         "axes " + (diagram[dataPlotNumber].Axis == PlotAxis.Y1 ? "x1y1" : "x1y2") +
                                         " with lines linestyle " + (singlePlotCounter++ % PlotColoring.OrderedHexCodes.Count() + 1) + " " +
                                         "t \"" + diagram[dataPlotNumber].GetDescription() + (headerElements.Length > 3 ? headerElements[colIndex] : "") +
                                         (diagram[dataPlotNumber].Axis == PlotAxis.Y1 ? " (y1)" : " (y2)") + "\"" +
                                         (dataPlotNumber < diagram.Count - 1 || colIndex < headerElements.Length - 1 ? ", \\" : ""));
                    }
                    // Reset parameters
                    sw.WriteLine("unset ylabel");
                    sw.WriteLine("unset y2label");
                }
                // Finish multiplot if it was used
                if (customDiagram.Plots.Count > 1)
                    sw.WriteLine("unset multiplot");
                // Tail
                sw.WriteLine(GenerateTailScriptCode());
            }
            // Generate short batch script
            string scriptPath;
            if (PlatformHelpers.IsLinux)
            {
                scriptPath = Path.Combine(outputDirPath, customDiagram.Name + ".sh");
                using (var sw = new StreamWriter(scriptPath))
                {
                    sw.WriteLine("#!/bin/bash");
                    sw.WriteLine("gnuplot " + customDiagram.Name + ".gp");
                }
                if (PlatformHelpers.IsLinux)
                    PlatformHelpers.SetExecutableAttribute(scriptPath, Config.Logger);
            }
            else
            {
                scriptPath = Path.Combine(outputDirPath, customDiagram.Name + ".cmd");
                using (var sw = new StreamWriter(scriptPath))
                {
                    sw.WriteLine("gnuplot " + customDiagram.Name + ".gp");
                }
            }
            scriptPaths.Add(scriptPath);
        }
        return scriptPaths;
    }

    #endregion

    #region Gnuplot execution

    public static void ExecuteScript(string script, Action<string> logger, bool changeWorkingDirToScript = true)
    {
        // Change dir to the one of the script
        var currentDir = Directory.GetCurrentDirectory();
        var scriptDir = Path.GetDirectoryName(script);
        if (!string.IsNullOrWhiteSpace(scriptDir))
            Directory.SetCurrentDirectory(scriptDir);
        // Check if command is available
        var commandName = Path.GetFileName(script.Split(' ')[0]);
        var argument = string.Join(" ", script.Split(' ').Skip(1));
        if (!CheckCommandExists(commandName))
        {
            // Not available
            logger("Command is not available: " + commandName);
        }
        else
        {
            // Execute it
            var process = new Process();
            var startInfo = new ProcessStartInfo();
            startInfo.WindowStyle = ProcessWindowStyle.Normal;
            startInfo.FileName = Path.GetFileName(commandName + (!string.IsNullOrWhiteSpace(argument) ? argument : ""));
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;
            logger("Executing " + startInfo.FileName);
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
            logger(process.StandardOutput.ReadToEnd());
        }
        // Change back to first dir
        if (!string.IsNullOrWhiteSpace(scriptDir))
            Directory.SetCurrentDirectory(currentDir);
    }

    #endregion

    #region Additional helpers

    /// <summary>
    /// Checks if a given command is available. If no extension is given, a .exe extension is assumed.
    /// </summary>
    /// <param name="command">The command to check.</param>
    /// <returns><code>true</code> if the command is available, <code>false</code> otherwise.</returns>
    private static bool CheckCommandExists(string command)
    {
        if (!Path.HasExtension(command))
            command += ".exe";
        if (File.Exists(command))
            return true;
        var paths = Environment.GetEnvironmentVariable("PATH").Split(Path.PathSeparator);
        foreach (var path in paths)
            if (File.Exists(Path.Combine(path, command)))
                return true;
        return false;
    }

    /// <summary>
    /// Counts the elements of a list within the given range.
    /// </summary>
    /// <param name="elements">The list of elements already in sorted order.</param>
    /// <param name="lb">The lower bound of the range.</param>
    /// <param name="lbInclusive">Indicates whether the lower bound is inclusive.</param>
    /// <param name="ub">The upper bound of the range.</param>
    /// <param name="ubInclusive">Indicates whether the upper bound is inclusive.</param>
    /// <returns>The number of elements within the given range.</returns>
    private static int CountElementsWithinRange(List<double> elements, double lb, bool lbInclusive, double ub, bool ubInclusive)
    {
        // Check bounds
        if (lb > ub)
            throw new ArgumentException("Lower bound has to be smaller than upper bound.");
        // Check inclusive or exclusive
        if (!lbInclusive)
            lb += double.Epsilon;
        if (!ubInclusive)
            ub += double.Epsilon;
        // Get indexes of the lower and upper bound to calculate elements within range
        var lbIndex = elements.Count / 2;
        var ubIndex = elements.Count / 2;
        lbIndex = elements.BinarySearch(lb);
        ubIndex = elements.BinarySearch(ub);
        // Keep in bounds of array (in case the element was not found)
        if (lbIndex < 0)
            lbIndex = ~lbIndex;
        if (ubIndex < 0)
            ubIndex = ~ubIndex;
        for (var i = lbIndex; i >= 0 && i < elements.Count && lb <= elements[i]; i--)
            lbIndex = i;
        for (var i = ubIndex; i < elements.Count && elements[i] <= ub; i++)
            ubIndex = i;
        // Calculate and return the elements within range
        var elementsInRange = ubIndex - lbIndex;
        return elementsInRange;
    }

    /// <summary>
    /// Filenamer to use for intermediate files used for custom plots.
    /// </summary>
    /// <param name="type">The type of the data.</param>
    /// <param name="stepLength">The step length that is active.</param>
    /// <returns>A filename that can be used.</returns>
    private static string IntermediateFileNamer(PlotDataContentType type, PlotTime stepLength, double averageWindowLength)
    {
        return "CustomPlotData" + type + stepLength + averageWindowLength.ToString(IOConstants.FORMATTER) + ".dat";
    }

    #endregion

    #endregion
}