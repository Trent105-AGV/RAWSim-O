using RAWSimO.Core.IO;
using RAWSimO.Core.Statistics;
using RAWSimO.Toolbox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RAWSimO.DataPreparation;

/// <summary>
/// Controls the plotting of well sortedness related data.
/// </summary>
public class WellSortednessProcessor
{
    /// <summary>
    /// Distinguishes different wellsortedness data types that can be plotted.
    /// </summary>
    public enum WellsortednessBaseDataType
    {
        /// <summary>
        /// Plots the frequency of the contained SKUs summed across all pods.
        /// </summary>
        SKUFreqSum,
        /// <summary>
        /// Plots the frequency of the contained SKUs averaged across all pods.
        /// </summary>
        SKUFreqAvg,
        /// <summary>
        /// Plots the frequency of the contained SKUs weighted by the number of units contained summed across all pods.
        /// </summary>
        ContentFreqSum,
        /// <summary>
        /// Plots the frequency of the contained SKUs weighted by the number of units contained averaged across all pods.
        /// </summary>
        ContentFreqAvg,
        /// <summary>
        /// Plots the number of trips to output-stations summed across all pods.
        /// </summary>
        OSTripsSum,
        /// <summary>
        /// Plots the number of trips to output-stations averaged across all pods.
        /// </summary>
        OSTripsAvg,
        /// <summary>
        /// Plots the speed score summed across all pods.
        /// </summary>
        PodSpeedSum,
        /// <summary>
        /// Plots the speed score averaged across all pods.
        /// </summary>
        PodSpeedAvg,
        /// <summary>
        /// Plots the utility score summed across all pods.
        /// </summary>
        PodUtilitySum,
        /// <summary>
        /// Plots the utility score averaged across all pods.
        /// </summary>
        PodUtilityAvg,
        /// <summary>
        /// Plots the combined score summed across all pods.
        /// </summary>
        PodScoreSum,
        /// <summary>
        /// Plots the combined score averaged across all pods.
        /// </summary>
        PodScoreAvg,
    }

    /// <summary>
    /// Defines which data will be overlayed over the wellsortedness plot.
    /// </summary>
    public enum WellsortednessPlotOverlay
    {
        /// <summary>
        /// No overlays.
        /// </summary>
        None,
        /// <summary>
        /// Overlays the bundle throughput time.
        /// </summary>
        BundleThroughputTime,
        /// <summary>
        /// Overlays the order throughput time.
        /// </summary>
        OrderThroughputTime,
        /// <summary>
        /// Overlays the bundle turnover time.
        /// </summary>
        BundleTurnoverTime,
        /// <summary>
        /// Overlays the order turnover time.
        /// </summary>
        OrderTurnoverTime,
        /// <summary>
        /// Overlays the bundle count during the time window.
        /// </summary>
        BundleCount,
        /// <summary>
        /// Overlays the order count during the time window.
        /// </summary>
        OrderCount,
        /// <summary>
        /// Overlays the distance traveled by the robots during the time window.
        /// </summary>
        DistanceTraveled,
    }

    /// <summary>
    /// The sub directory name for storing the intermediate dat files.
    /// </summary>
    public const string DAT_FILE_SUB_DIR = "wellsortednessfiles";

    /// <summary>
    /// Returns a relative file name to use while also ensuring that the sub-directory exists.
    /// </summary>
    /// <param name="parentDir">The current directory.</param>
    /// <param name="dataType">The data type that the dat-file shall contain.</param>
    /// <param name="fileNumber">The current number of the dat file.</param>
    /// <param name="timestamp">The corresponding timestamp.</param>
    /// <returns>A relative path to a dat-file exposing the given data.</returns>
    public static string GetRelDatFileName(string parentDir, WellsortednessBaseDataType dataType, double timestamp)
    {
        var dirName = Path.Combine(parentDir, DAT_FILE_SUB_DIR);
        while (!Directory.Exists(dirName))
        {
            Directory.CreateDirectory(dirName);
            Thread.Sleep(100);
        }

        return Path.Combine(dirName, "ws_" + dataType + "_" + timestamp.ToString(IOConstants.EXPORT_FORMAT_SHORTER, IOConstants.FORMATTER) + ".dat");
    }

    /// <summary>
    /// Extracts the desired value from a wellsortedness datapoint.
    /// </summary>
    /// <param name="datapoint">The datapoint.</param>
    /// <param name="dataType">The type of data to extract.</param>
    /// <returns>The value for the data-type for the given datapoint.</returns>
    private static double GetWellsortednessValue(WellSortednessPathTimeTuple datapoint, WellsortednessBaseDataType dataType)
    {
        return dataType switch
        {
            WellsortednessBaseDataType.SKUFreqSum => datapoint.SKUFrequencySum,
            WellsortednessBaseDataType.SKUFreqAvg => datapoint.PodCount == 0
                ? 0
                : datapoint.SKUFrequencySum / datapoint.PodCount,
            WellsortednessBaseDataType.ContentFreqSum => datapoint.ContentFrequencySum,
            WellsortednessBaseDataType.ContentFreqAvg => datapoint.PodCount == 0
                ? 0
                : datapoint.ContentFrequencySum / datapoint.PodCount,
            WellsortednessBaseDataType.OSTripsSum => datapoint.OutputStationTripsSum,
            WellsortednessBaseDataType.OSTripsAvg => datapoint.PodCount == 0
                ? 0
                : datapoint.OutputStationTripsSum / datapoint.PodCount,
            WellsortednessBaseDataType.PodSpeedSum => datapoint.PodSpeedSum,
            WellsortednessBaseDataType.PodSpeedAvg => datapoint.PodCount == 0
                ? 0
                : datapoint.PodSpeedSum / datapoint.PodCount,
            WellsortednessBaseDataType.PodUtilitySum => datapoint.PodUtilitySum,
            WellsortednessBaseDataType.PodUtilityAvg => datapoint.PodCount == 0
                ? 0
                : datapoint.PodUtilitySum / datapoint.PodCount,
            WellsortednessBaseDataType.PodScoreSum => datapoint.PodCombinedScoreSum,
            WellsortednessBaseDataType.PodScoreAvg => datapoint.PodCount == 0
                ? 0
                : datapoint.PodCombinedScoreSum / datapoint.PodCount,
            _ => throw new ArgumentException("Unknown data type: " + dataType)
        };
    }

    /// <summary>
    /// Plots all well sortedness graphs the processor can find in a direct sub-directory of the given one.
    /// </summary>
    /// <param name="parentDirectory"></param>
    /// <param name="dataType">The type of data to plot.</param>
    /// <param name="overlayType">Indicates which overlay will be plotted.</param>
    /// <param name="pathtimeAggregationLength">The length in path time to group different rows of pods.</param>
    /// <param name="timeAggregationLength">The legnth in time to aggregate results by.</param>
    public static void PlotAll(string parentDirectory, WellsortednessBaseDataType dataType, WellsortednessPlotOverlay overlayType, double pathtimeAggregationLength, double timeAggregationLength)
    {
        // Get directories
        var dirs = Directory.EnumerateDirectories(parentDirectory).ToArray();
        // Iterate all dirs
        Parallel.ForEach(dirs, new ParallelOptions { MaxDegreeOfParallelism = 1 /* No parallelization for now, this may be a reason for weird behavior that was observed */ }, (string dir) =>
        {
            // Log
            Console.WriteLine("Preparing dir: " + dir + " ...");
            // Check for the file
            var dataFile = Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.WellSortednessPollingRaw]);
            if (File.Exists(dataFile))
            {
                // --> Parse the instance and config names
                string instanceName;
                using (var sr = new StreamReader(Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.InstanceName])))
                {
                    var instanceNameLine = "";
                    while (string.IsNullOrWhiteSpace(instanceNameLine = sr.ReadLine())) { /* Nothing to do - the head of the loop does the work */ }
                    instanceName = instanceNameLine.Trim();
                }
                // Fetch config name
                string configName;
                using (var sr = new StreamReader(Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.ControllerName])))
                {
                    var configNameLine = "";
                    while (string.IsNullOrWhiteSpace(configNameLine = sr.ReadLine())) { /* Nothing to do - the head of the loop does the work */ }
                    configName = configNameLine.Trim();
                }
                // --> Parse the data
                var datapoints = new List<WellSortednessDatapoint>();
                using (var sr = new StreamReader(dataFile))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        // Trim
                        line = line.Trim();
                        // Skip empty or comment lines
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith(IOConstants.COMMENT_LINE))
                            continue;
                        // Actually parse the line
                        datapoints.Add(new WellSortednessDatapoint(line));
                    }
                }
                // Sort all datapoints
                datapoints = datapoints.OrderBy(d => d.TimeStamp).ToList();
                // --> Aggregate datapoints per time window (if desired)
                var aggregateTime = datapoints.Select(d => d.TimeStamp).Distinct().AllOrderedTuple((double first, double second) => second - first < timeAggregationLength);
                if (aggregateTime)
                {
                    var currentWindowEnd = timeAggregationLength;
                    var aggregatedDatapoints = new List<WellSortednessDatapoint>();
                    while (datapoints.Any())
                    {
                        // Get datapoints of current window
                        var windowDatapoints = datapoints.TakeWhile(d => d.TimeStamp < currentWindowEnd).ToList();
                        // Check whether there are any datapoints in the timewindow
                        if (windowDatapoints.Any())
                        {
                            // Remove them from the original list
                            datapoints.RemoveAll(d => d.TimeStamp < currentWindowEnd);
                            // Convert path time info
                            var groups = windowDatapoints.SelectMany(d => d.PathTimeFrequencies).GroupBy(d => d.PathTime);
                            // Create aggregated datapoints
                            aggregatedDatapoints.Add(new WellSortednessDatapoint(
                                // New timestamp is right in the middle of the current window
                                currentWindowEnd - timeAggregationLength / 2.0,
                                groups.Select(g => new WellSortednessPathTimeTuple
                                {
                                    PathTime = g.Key,
                                    PodCount = g.Average(e => e.PodCount),
                                    // Make sure there is only one storage location count for all datapoints in the time window for this row
                                    StorageLocationCount = g.Select(e => e.StorageLocationCount).Distinct().Single(),
                                    ContentFrequencySum = g.Average(e => e.ContentFrequencySum),
                                    OutputStationTripsSum = g.Average(e => e.OutputStationTripsSum),
                                    SKUFrequencySum = g.Average(e => e.SKUFrequencySum),
                                    PodSpeedSum = g.Average(e => e.PodSpeedSum),
                                    PodUtilitySum = g.Average(e => e.PodUtilitySum),
                                    PodCombinedScoreSum = g.Average(e => e.PodCombinedScoreSum),
                                }).ToList()));
                        }
                        // Move time window
                        currentWindowEnd += timeAggregationLength;
                    }
                    // Replace original datapoints with aggregated ones
                    datapoints = aggregatedDatapoints;
                }
                // --> Aggregate datapoints per path time length (if desired)
                if (pathtimeAggregationLength > 0)
                {
                    foreach (var datapoint in datapoints)
                    {
                        var currentWindowEnd = pathtimeAggregationLength;
                        datapoint.PathTimeFrequencies = datapoint.PathTimeFrequencies.OrderBy(d => d.PathTime).ToList();
                        var aggregatedDatapoints = new List<WellSortednessPathTimeTuple>();
                        while (datapoint.PathTimeFrequencies.Any())
                        {
                            // Get datapoints of current window
                            var windowDatapoints = datapoint.PathTimeFrequencies.TakeWhile(d => d.PathTime < currentWindowEnd).ToList();
                            // Check whether there are any datapoints in the timewindow
                            if (windowDatapoints.Any())
                            {
                                // Remove them from the original list
                                datapoint.PathTimeFrequencies.RemoveAll(d => d.PathTime < currentWindowEnd);
                                // Create aggregated datapoints
                                aggregatedDatapoints.Add(new WellSortednessPathTimeTuple
                                {
                                    // New pathtime is right in the middle of the current window
                                    PathTime = currentWindowEnd - pathtimeAggregationLength / 2.0,
                                    PodCount = windowDatapoints.Sum(d => d.PodCount),
                                    // Make sure there is only one storage location count for all datapoints in the time window for this row
                                    StorageLocationCount = windowDatapoints.Sum(e => e.StorageLocationCount),
                                    ContentFrequencySum = windowDatapoints.Sum(d => d.ContentFrequencySum),
                                    OutputStationTripsSum = windowDatapoints.Sum(d => d.OutputStationTripsSum),
                                    SKUFrequencySum = windowDatapoints.Sum(d => d.SKUFrequencySum),
                                    PodSpeedSum = windowDatapoints.Sum(d => d.PodSpeedSum),
                                    PodUtilitySum = windowDatapoints.Sum(d => d.PodUtilitySum),
                                    PodCombinedScoreSum = windowDatapoints.Sum(d => d.PodCombinedScoreSum),
                                });
                            }
                            // Move time window
                            currentWindowEnd += pathtimeAggregationLength;
                        }
                        // Replace original datapoints with aggregated ones
                        datapoint.PathTimeFrequencies = aggregatedDatapoints;
                    }
                }
                // --> Parse information about throughput times
                var orderDataFile = Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.OrderProgressionRaw]);
                var bundleDataFile = Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.BundleProgressionRaw]);
                var distanceDataFile = Path.Combine(dir, IOConstants.StatFileNames[IOConstants.StatFile.TraveledDistanceProgressionRaw]);
                var orderThroughputTimes = new Dictionary<double, double>();
                var bundleThroughputTimes = new Dictionary<double, double>();
                var orderTurnoverTimes = new Dictionary<double, double>();
                var bundleTurnoverTimes = new Dictionary<double, double>();
                var orderCounts = new Dictionary<double, int>();
                var bundleCounts = new Dictionary<double, int>();
                var distanceTraveled = new Dictionary<double, double>();
                // Get information about order handling over time
                using (var sr = new StreamReader(orderDataFile))
                {
                    // Parse order information
                    var orderHandledDatapoints = new List<OrderHandledDatapoint>(); string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        // Trim
                        line = line.Trim();
                        // Skip empty or comment lines
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith(IOConstants.COMMENT_LINE))
                            continue;
                        // Actually parse the line
                        orderHandledDatapoints.Add(new OrderHandledDatapoint(line));
                    }
                    // Obtain values for the different time-stamps
                    orderHandledDatapoints = orderHandledDatapoints.OrderBy(d => d.TimeStamp).ToList();
                    // Get data per timestamp
                    double previousThroughputValue = 0; double previousTurnoverValue = 0;
                    foreach (var timestamp in datapoints.Select(d => d.TimeStamp).Distinct().OrderBy(d => d))
                    {
                        var datapointsOfSection = orderHandledDatapoints.TakeWhile(d => d.TimeStamp <= timestamp);
                        // Measure count
                        orderCounts[timestamp] = datapointsOfSection.Count();
                        // Measure timings
                        if (datapointsOfSection.Any())
                        {
                            // Measure avg. time
                            orderThroughputTimes[timestamp] = datapointsOfSection.Average(d => d.ThroughputTime);
                            previousThroughputValue = orderThroughputTimes[timestamp];
                            orderTurnoverTimes[timestamp] = datapointsOfSection.Average(d => d.TurnoverTime);
                            previousTurnoverValue = orderTurnoverTimes[timestamp];
                        }
                        else
                        {
                            // No new measurement - use the one from the point before
                            orderThroughputTimes[timestamp] = previousThroughputValue;
                            orderTurnoverTimes[timestamp] = previousTurnoverValue;
                        }
                        orderHandledDatapoints = orderHandledDatapoints.Skip(datapointsOfSection.Count()).ToList();
                    }
                }
                // Get information about bundle handling over time
                using (var sr = new StreamReader(bundleDataFile))
                {
                    // Parse bundle information
                    var bundleHandledDatapoints = new List<BundleHandledDatapoint>(); string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        // Trim
                        line = line.Trim();
                        // Skip empty or comment lines
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith(IOConstants.COMMENT_LINE))
                            continue;
                        // Actually parse the line
                        bundleHandledDatapoints.Add(new BundleHandledDatapoint(line));
                    }
                    // Obtain values for the different time-stamps
                    bundleHandledDatapoints = bundleHandledDatapoints.OrderBy(d => d.TimeStamp).ToList();
                    // Get data per timestamp
                    double previousThroughputValue = 0; double previousTurnoverValue = 0;
                    foreach (var timestamp in datapoints.Select(d => d.TimeStamp).Distinct().OrderBy(d => d))
                    {
                        var datapointsOfSection = bundleHandledDatapoints.TakeWhile(d => d.TimeStamp <= timestamp);
                        // Measure count
                        bundleCounts[timestamp] = datapointsOfSection.Count();
                        // Measure timings
                        if (datapointsOfSection.Any())
                        {
                            // Measure avg. time
                            bundleThroughputTimes[timestamp] = datapointsOfSection.Average(d => d.ThroughputTime);
                            previousThroughputValue = bundleThroughputTimes[timestamp];
                            bundleTurnoverTimes[timestamp] = datapointsOfSection.Average(d => d.ThroughputTime);
                            previousTurnoverValue = bundleTurnoverTimes[timestamp];
                        }
                        else
                        {
                            // No new measurement - use the one from the point before
                            bundleThroughputTimes[timestamp] = previousThroughputValue;
                            bundleTurnoverTimes[timestamp] = previousTurnoverValue;
                        }
                        bundleHandledDatapoints = bundleHandledDatapoints.Skip(datapointsOfSection.Count()).ToList();
                    }
                }
                // Get information about distance traveled over time
                using (var sr = new StreamReader(distanceDataFile))
                {
                    // Parse datapoints
                    var distanceTraveledDatapoints = new List<DistanceDatapoint>(); string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        // Trim
                        line = line.Trim();
                        // Skip empty or comment lines
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith(IOConstants.COMMENT_LINE))
                            continue;
                        // Actually parse the line
                        distanceTraveledDatapoints.Add(new DistanceDatapoint(line));
                    }
                    // Obtain values for the different time-stamps
                    distanceTraveledDatapoints = distanceTraveledDatapoints.OrderBy(d => d.TimeStamp).ToList();
                    // Get data per timestamp
                    foreach (var timestamp in datapoints.Select(d => d.TimeStamp).Distinct().OrderBy(d => d))
                    {
                        // Get datapoints of time window
                        var datapointsOfSection = distanceTraveledDatapoints.TakeWhile(d => d.TimeStamp <= timestamp);
                        // Measure distance traveled within time window
                        distanceTraveled[timestamp] = datapointsOfSection.Sum(d => d.DistanceTraveled);
                        // Remove measured datapoints
                        distanceTraveledDatapoints = distanceTraveledDatapoints.Skip(datapointsOfSection.Count()).ToList();
                    }
                }
                // --> Get information about the storage locations per pathtime
                var storageLocationsPerPathTime = datapoints.First().PathTimeFrequencies.ToDictionary(k => k.PathTime, v => (int)v.StorageLocationCount);
                // Log
                Console.WriteLine("Parsed " + datapoints.Count + " timestamps - plotting ...");
                // Initiate dat-file generator
                var maxPathTime = double.NegativeInfinity;
                var maxValue = double.NegativeInfinity;
                var fileGenerator =
                    (IDictionary<double, string> files) =>
                    {
                        // Write a dat file for all available timestamps
                        foreach (var timeEntry in datapoints)
                        {
                            // Write the dat file for the plot at this timestamp
                            var fileName = GetRelDatFileName(dir, dataType, timeEntry.TimeStamp);
                            files[timeEntry.TimeStamp] = fileName;
                            using (var sw = new StreamWriter(fileName))
                            {
                                sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " pathtime " + dataType);
                                foreach (var pathTimeEntry in timeEntry.PathTimeFrequencies)
                                {
                                    // Write the single line for the respective pathtime
                                    sw.WriteLine(
                                        pathTimeEntry.PathTime.ToString(IOConstants.FORMATTER) + IOConstants.GNU_PLOT_VALUE_SPLIT +
                                        GetWellsortednessValue(pathTimeEntry, dataType).ToString(IOConstants.FORMATTER));
                                    // Keep track of max values for setting ranges later on
                                    if (pathTimeEntry.PathTime > maxPathTime)
                                        maxPathTime = pathTimeEntry.PathTime;
                                    if (GetWellsortednessValue(pathTimeEntry, dataType) > maxValue)
                                        maxValue = GetWellsortednessValue(pathTimeEntry, dataType);
                                }
                            }
                        }
                    };
                // --> Prepare intermediate files
                // Prepare frequency files
                var frequencyDataFiles = new Dictionary<double, string>();
                fileGenerator(frequencyDataFiles);
                // Prepare throughput intermediate file
                var throughputDatafile = "wellsortednessMetaData.dat";
                using (var sw = new StreamWriter(Path.Combine(dir, throughputDatafile), false))
                {
                    sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " timestamp bundlethroughput orderthroughput bundlecount ordercount distancetraveled");
                    foreach (var timestamp in datapoints.Select(d => d.TimeStamp).OrderBy(d => d))
                    {
                        // Write the single line for the respective timestamp
                        sw.WriteLine(
                            timestamp.ToString(IOConstants.FORMATTER) + IOConstants.GNU_PLOT_VALUE_SPLIT +
                            bundleThroughputTimes[timestamp].ToString(IOConstants.FORMATTER) + IOConstants.GNU_PLOT_VALUE_SPLIT +
                            orderThroughputTimes[timestamp].ToString(IOConstants.FORMATTER) + IOConstants.GNU_PLOT_VALUE_SPLIT +
                            bundleTurnoverTimes[timestamp].ToString(IOConstants.FORMATTER) + IOConstants.GNU_PLOT_VALUE_SPLIT +
                            orderTurnoverTimes[timestamp].ToString(IOConstants.FORMATTER) + IOConstants.GNU_PLOT_VALUE_SPLIT +
                            bundleCounts[timestamp].ToString(IOConstants.FORMATTER) + IOConstants.GNU_PLOT_VALUE_SPLIT +
                            orderCounts[timestamp].ToString(IOConstants.FORMATTER) + IOConstants.GNU_PLOT_VALUE_SPLIT +
                            distanceTraveled[timestamp].ToString(IOConstants.FORMATTER));
                    }
                }
                // Prepare storage locations intermediate file
                var storageLocationsDataFile = "wellsortednessStorageLocations.dat";
                using (var sw = new StreamWriter(Path.Combine(dir, storageLocationsDataFile), false))
                {
                    sw.WriteLine(IOConstants.GNU_PLOT_COMMENT_LINE + " pathtime storagelocationcount");
                    foreach (var pathTimeTuple in storageLocationsPerPathTime.OrderBy(kvp => kvp.Key))
                    {
                        // Write the single line for the respective pathtime
                        sw.WriteLine(
                            pathTimeTuple.Key.ToString(IOConstants.FORMATTER) + IOConstants.GNU_PLOT_VALUE_SPLIT +
                            pathTimeTuple.Value.ToString(IOConstants.FORMATTER));
                    }
                }
                // Generate plot script
                var plotScriptName = Path.Combine(dir, "wellsortedness-" + dataType + "-" + overlayType + ".gp");
                using (var sw = new StreamWriter(plotScriptName))
                {
                    // Init
                    sw.WriteLine("reset");
                    sw.WriteLine("# Output definition");
                    sw.WriteLine("set terminal pdfcairo enhanced size 7, 3 font \"Consolas, 12\"");
                    sw.WriteLine("set lmargin 13");
                    sw.WriteLine("set rmargin 13");
                    // Define first parameters
                    sw.WriteLine("# Parameters");
                    sw.WriteLine("set key right top Right");
                    sw.WriteLine("set xlabel \"PathTime\"");
                    sw.WriteLine("set xrange [" + 0.ToString(IOConstants.FORMATTER) + ":" + (maxPathTime * 1.1).ToString(IOConstants.FORMATTER) + "]");
                    sw.WriteLine("set grid");
                    sw.WriteLine("set style fill solid 0.75");
                    // Define line styles
                    sw.WriteLine("# Line-Styles");
                    var overlayColor = PlotColors.MediumGreen;
                    switch (overlayType)
                    {
                        case WellsortednessPlotOverlay.BundleThroughputTime: overlayColor = PlotColors.MediumTurquoise; break;
                        case WellsortednessPlotOverlay.OrderThroughputTime: overlayColor = PlotColors.MediumGreen; break;
                        case WellsortednessPlotOverlay.BundleTurnoverTime: overlayColor = PlotColors.MediumBlue; break;
                        case WellsortednessPlotOverlay.OrderTurnoverTime: overlayColor = PlotColors.MediumOrange; break;
                        case WellsortednessPlotOverlay.BundleCount: overlayColor = PlotColors.MediumViolet; break;
                        case WellsortednessPlotOverlay.OrderCount: overlayColor = PlotColors.MediumRed; break;
                        case WellsortednessPlotOverlay.DistanceTraveled: overlayColor = PlotColors.MediumYellow; break;
                        case WellsortednessPlotOverlay.None:
                        default: /* Color will not be used anyway */ break;
                    }
                    sw.WriteLine("set style line 1 linetype 1 linecolor rgb \"" + PlotColoring.GetHexCode(PlotColors.MediumBlue) + "\" linewidth 1");
                    sw.WriteLine("set style line 2 linetype 1 linecolor rgb \"" + PlotColoring.GetHexCode(overlayColor) + "\" linewidth 3");
                    sw.WriteLine("set style line 3 linetype 1 linecolor rgb \"" + PlotColoring.GetHexCode(PlotColors.MediumGrey) + "\" linewidth 2 pt 2");
                    // Add pathtime plot
                    sw.WriteLine("set output \"wellsortednessGraphStorageLocations.pdf\"");
                    sw.WriteLine("set yrange [" + 0.ToString(IOConstants.FORMATTER) + ":" + (storageLocationsPerPathTime.Values.Max() * 1.1).ToString(IOConstants.FORMATTER) + "]");
                    sw.WriteLine("set ylabel \"Count\"");
                    sw.WriteLine("plot \\");
                    sw.WriteLine("\"" + storageLocationsDataFile + "\" u 1:2 w boxes linestyle 1 t \"storage locations\"");
                    // Define parameters for the frequency plots
                    switch (overlayType)
                    {
                        case WellsortednessPlotOverlay.None: /* Nothing to do */ break;
                        case WellsortednessPlotOverlay.BundleThroughputTime:
                        {
                            sw.WriteLine("set x2label \"Simulation time\"");
                            sw.WriteLine("set y2label \"Throughput time\"");
                            sw.WriteLine("set x2tics");
                            sw.WriteLine("set y2tics");
                            sw.WriteLine("set x2range [" + 0.ToString(IOConstants.FORMATTER) + ":" + bundleThroughputTimes.Max(t => t.Key).ToString(IOConstants.FORMATTER) + "]");
                            sw.WriteLine("set y2range [" + (0.9 * bundleThroughputTimes.Min(t => t.Value)).ToString(IOConstants.FORMATTER) + ":" +
                                         (1.1 * bundleThroughputTimes.Max(t => t.Value)).ToString(IOConstants.FORMATTER) + "]");
                        }
                            break;
                        case WellsortednessPlotOverlay.OrderThroughputTime:
                        {
                            sw.WriteLine("set x2label \"Simulation time\"");
                            sw.WriteLine("set y2label \"Throughput time\"");
                            sw.WriteLine("set x2tics");
                            sw.WriteLine("set y2tics");
                            sw.WriteLine("set x2range [" + 0.ToString(IOConstants.FORMATTER) + ":" + orderThroughputTimes.Max(t => t.Key).ToString(IOConstants.FORMATTER) + "]");
                            sw.WriteLine("set y2range [" + (0.9 * orderThroughputTimes.Min(t => t.Value)).ToString(IOConstants.FORMATTER) + ":" +
                                         (1.1 * orderThroughputTimes.Max(t => t.Value)).ToString(IOConstants.FORMATTER) + "]");
                        }
                            break;
                        case WellsortednessPlotOverlay.BundleTurnoverTime:
                        {
                            sw.WriteLine("set x2label \"Simulation time\"");
                            sw.WriteLine("set y2label \"Turnover time\"");
                            sw.WriteLine("set x2tics");
                            sw.WriteLine("set y2tics");
                            sw.WriteLine("set x2range [" + 0.ToString(IOConstants.FORMATTER) + ":" + bundleTurnoverTimes.Max(t => t.Key).ToString(IOConstants.FORMATTER) + "]");
                            sw.WriteLine("set y2range [" + (0.9 * bundleTurnoverTimes.Min(t => t.Value)).ToString(IOConstants.FORMATTER) + ":" +
                                         (1.1 * bundleTurnoverTimes.Max(t => t.Value)).ToString(IOConstants.FORMATTER) + "]");
                        }
                            break;
                        case WellsortednessPlotOverlay.OrderTurnoverTime:
                        {
                            sw.WriteLine("set x2label \"Simulation time\"");
                            sw.WriteLine("set y2label \"Turnover time\"");
                            sw.WriteLine("set x2tics");
                            sw.WriteLine("set y2tics");
                            sw.WriteLine("set x2range [" + 0.ToString(IOConstants.FORMATTER) + ":" + orderTurnoverTimes.Max(t => t.Key).ToString(IOConstants.FORMATTER) + "]");
                            sw.WriteLine("set y2range [" + (0.9 * orderTurnoverTimes.Min(t => t.Value)).ToString(IOConstants.FORMATTER) + ":" +
                                         (1.1 * orderTurnoverTimes.Max(t => t.Value)).ToString(IOConstants.FORMATTER) + "]");
                        }
                            break;
                        case WellsortednessPlotOverlay.BundleCount:
                        {
                            sw.WriteLine("set x2label \"Simulation time\"");
                            sw.WriteLine("set y2label \"Bundle count\"");
                            sw.WriteLine("set x2tics");
                            sw.WriteLine("set y2tics");
                            sw.WriteLine("set x2range [" + 0.ToString(IOConstants.FORMATTER) + ":" + bundleCounts.Max(t => t.Key).ToString(IOConstants.FORMATTER) + "]");
                            sw.WriteLine("set y2range [" + (0.9 * bundleCounts.Min(t => t.Value)).ToString(IOConstants.FORMATTER) + ":" +
                                         (1.1 * bundleCounts.Max(t => t.Value)).ToString(IOConstants.FORMATTER) + "]");
                        }
                            break;
                        case WellsortednessPlotOverlay.OrderCount:
                        {
                            sw.WriteLine("set x2label \"Simulation time\"");
                            sw.WriteLine("set y2label \"Order count\"");
                            sw.WriteLine("set x2tics");
                            sw.WriteLine("set y2tics");
                            sw.WriteLine("set x2range [" + 0.ToString(IOConstants.FORMATTER) + ":" + orderCounts.Max(t => t.Key).ToString(IOConstants.FORMATTER) + "]");
                            sw.WriteLine("set y2range [" + (0.9 * orderCounts.Min(t => t.Value)).ToString(IOConstants.FORMATTER) + ":" +
                                         (1.1 * orderCounts.Max(t => t.Value)).ToString(IOConstants.FORMATTER) + "]");
                        }
                            break;
                        case WellsortednessPlotOverlay.DistanceTraveled:
                        {
                            sw.WriteLine("set x2label \"Simulation time\"");
                            sw.WriteLine("set y2label \"Distance traveled (in m)\"");
                            sw.WriteLine("set x2tics");
                            sw.WriteLine("set y2tics");
                            sw.WriteLine("set x2range [" + 0.ToString(IOConstants.FORMATTER) + ":" + distanceTraveled.Max(t => t.Key).ToString(IOConstants.FORMATTER) + "]");
                            sw.WriteLine("set y2range [" + (0.9 * distanceTraveled.Min(t => t.Value)).ToString(IOConstants.FORMATTER) + ":" +
                                         (1.1 * distanceTraveled.Max(t => t.Value)).ToString(IOConstants.FORMATTER) + "]");
                        }
                            break;
                        default: throw new ArgumentException("Unknown overlay type: " + overlayType);
                    }
                    // Quick define script generator function
                    var plotScriptGenAction = (KeyValuePair<double, string> plotdatafile, int datIndex) =>
                    {
                        var datFile = Path.GetFileName(Path.GetDirectoryName(plotdatafile.Value)) + "/" + Path.GetFileName(plotdatafile.Value);
                        sw.WriteLine("set title \"" + instanceName + " / " + configName + " / " + TimeSpan.FromSeconds(plotdatafile.Key).ToString(IOConstants.TIMESPAN_FORMAT_HUMAN_READABLE_DAYS) + "\"");
                        switch (overlayType)
                        {
                            case WellsortednessPlotOverlay.None:
                            {
                                sw.WriteLine("plot \\");
                                sw.WriteLine("\"" + datFile + "\" u 1:" + datIndex + " w boxes linestyle 1 t \"well-sortedness\"");
                            }
                                break;
                            case WellsortednessPlotOverlay.BundleThroughputTime:
                            {
                                sw.WriteLine("overlayx=" + plotdatafile.Key.ToString(IOConstants.FORMATTER));
                                sw.WriteLine("overlayy=" + bundleThroughputTimes[plotdatafile.Key].ToString(IOConstants.FORMATTER));
                                sw.WriteLine("plot \\");
                                sw.WriteLine("\"" + datFile + "\" u 1:" + datIndex + " w boxes linestyle 1 t \"well-sortedness\", \\");
                                sw.WriteLine("\"" + throughputDatafile + "\" u 1:2 w steps axes x2y2 linestyle 2 t \"bundle throughput time\", \\");
                                sw.WriteLine("\"+\" u (overlayx):(overlayy) w points axes x2y2 linestyle 3 t \"current time\"");
                            }
                                break;
                            case WellsortednessPlotOverlay.OrderThroughputTime:
                            {
                                sw.WriteLine("overlayx=" + plotdatafile.Key.ToString(IOConstants.FORMATTER));
                                sw.WriteLine("overlayy=" + orderThroughputTimes[plotdatafile.Key].ToString(IOConstants.FORMATTER));
                                sw.WriteLine("plot \\");
                                sw.WriteLine("\"" + datFile + "\" u 1:" + datIndex + " w boxes linestyle 1 t \"well-sortedness\", \\");
                                sw.WriteLine("\"" + throughputDatafile + "\" u 1:3 w steps axes x2y2 linestyle 2 t \"order throughput time\", \\");
                                sw.WriteLine("\"+\" u (overlayx):(overlayy) w points axes x2y2 linestyle 3 t \"current time\"");
                            }
                                break;
                            case WellsortednessPlotOverlay.BundleTurnoverTime:
                            {
                                sw.WriteLine("overlayx=" + plotdatafile.Key.ToString(IOConstants.FORMATTER));
                                sw.WriteLine("overlayy=" + bundleTurnoverTimes[plotdatafile.Key].ToString(IOConstants.FORMATTER));
                                sw.WriteLine("plot \\");
                                sw.WriteLine("\"" + datFile + "\" u 1:" + datIndex + " w boxes linestyle 1 t \"well-sortedness\", \\");
                                sw.WriteLine("\"" + throughputDatafile + "\" u 1:4 w steps axes x2y2 linestyle 2 t \"bundle turnover time\", \\");
                                sw.WriteLine("\"+\" u (overlayx):(overlayy) w points axes x2y2 linestyle 3 t \"current time\"");
                            }
                                break;
                            case WellsortednessPlotOverlay.OrderTurnoverTime:
                            {
                                sw.WriteLine("overlayx=" + plotdatafile.Key.ToString(IOConstants.FORMATTER));
                                sw.WriteLine("overlayy=" + orderTurnoverTimes[plotdatafile.Key].ToString(IOConstants.FORMATTER));
                                sw.WriteLine("plot \\");
                                sw.WriteLine("\"" + datFile + "\" u 1:" + datIndex + " w boxes linestyle 1 t \"well-sortedness\", \\");
                                sw.WriteLine("\"" + throughputDatafile + "\" u 1:5 w steps axes x2y2 linestyle 2 t \"order turnover time\", \\");
                                sw.WriteLine("\"+\" u (overlayx):(overlayy) w points axes x2y2 linestyle 3 t \"current time\"");
                            }
                                break;
                            case WellsortednessPlotOverlay.BundleCount:
                            {
                                sw.WriteLine("overlayx=" + plotdatafile.Key.ToString(IOConstants.FORMATTER));
                                sw.WriteLine("overlayy=" + bundleCounts[plotdatafile.Key].ToString(IOConstants.FORMATTER));
                                sw.WriteLine("plot \\");
                                sw.WriteLine("\"" + datFile + "\" u 1:" + datIndex + " w boxes linestyle 1 t \"well-sortedness\", \\");
                                sw.WriteLine("\"" + throughputDatafile + "\" u 1:6 w steps axes x2y2 linestyle 2 t \"bundles stored\", \\");
                                sw.WriteLine("\"+\" u (overlayx):(overlayy) w points axes x2y2 linestyle 3 t \"current time\"");
                            }
                                break;
                            case WellsortednessPlotOverlay.OrderCount:
                            {
                                sw.WriteLine("overlayx=" + plotdatafile.Key.ToString(IOConstants.FORMATTER));
                                sw.WriteLine("overlayy=" + orderCounts[plotdatafile.Key].ToString(IOConstants.FORMATTER));
                                sw.WriteLine("plot \\");
                                sw.WriteLine("\"" + datFile + "\" u 1:" + datIndex + " w boxes linestyle 1 t \"well-sortedness\", \\");
                                sw.WriteLine("\"" + throughputDatafile + "\" u 1:7 w steps axes x2y2 linestyle 2 t \"orders fulfilled\", \\");
                                sw.WriteLine("\"+\" u (overlayx):(overlayy) w points axes x2y2 linestyle 3 t \"current time\"");
                            }
                                break;
                            case WellsortednessPlotOverlay.DistanceTraveled:
                            {
                                sw.WriteLine("overlayx=" + plotdatafile.Key.ToString(IOConstants.FORMATTER));
                                sw.WriteLine("overlayy=" + distanceTraveled[plotdatafile.Key].ToString(IOConstants.FORMATTER));
                                sw.WriteLine("plot \\");
                                sw.WriteLine("\"" + datFile + "\" u 1:" + datIndex + " w boxes linestyle 1 t \"well-sortedness\", \\");
                                sw.WriteLine("\"" + throughputDatafile + "\" u 1:8 w steps axes x2y2 linestyle 2 t \"distance traveled\", \\");
                                sw.WriteLine("\"+\" u (overlayx):(overlayy) w points axes x2y2 linestyle 3 t \"current time\"");
                            }
                                break;
                            default:
                                break;
                        }
                    };
                    // Add frequency plots
                    sw.WriteLine("set output \"ws-" + dataType + "-" + overlayType + ".pdf\"");
                    sw.WriteLine("set yrange [" + 0.ToString(IOConstants.FORMATTER) + ":" + (maxValue * 1.1).ToString(IOConstants.FORMATTER) + "]");
                    sw.WriteLine("set ylabel \"Value\"");
                    foreach (var plotdatafile in frequencyDataFiles.OrderBy(p => p.Key))
                    {
                        plotScriptGenAction(plotdatafile, 2);
                    }
                    sw.WriteLine("reset");
                    sw.WriteLine("exit");
                }
                var commandScriptName = Path.Combine(dir, Path.GetFileNameWithoutExtension(plotScriptName) + ".cmd");
                using (var sw = new StreamWriter(commandScriptName))
                {
                    sw.WriteLine("gnuplot " + Path.GetFileName(plotScriptName));
                }
                // Log
                Console.WriteLine("Calling plot script ...");
                // Execute plot script
                DataProcessor.ExecuteScript(commandScriptName, (string msg) => { Console.WriteLine(msg); });
            }
        });
    }
}