using RAWSimO.Core;
using RAWSimO.Core.Control;
using RAWSimO.Core.IO;
using RAWSimO.Core.Randomization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RAWSimO.Playground;

internal class JenkinsHandler
{
    /// <summary>
    /// The list of CLI arguments with a corresponding short explanation.
    /// </summary>
    public static readonly Tuple<string, string>[] CliArgs =
    [
        new("Instance","The path to the instance file"),
        new("Setting","The path to the setting configuration file"),
        new("ControlConfig","The path to the controller configuration file"),
        new("StatisticsDir","The path to the directory into which the results are written"),
        new("Seed","The seed to pass to the simulator"),
        new("Build#","The number of the build")
    ];

    public static void HandleJenkinsCall(string[] args)
    {
        // On invalid arguments show info
        if (args.Length != CliArgs.Length)
        {
            Console.WriteLine("Usage: RAWSimO.CLI.exe " + string.Join(" ", CliArgs.Select(a => "<" + a.Item1 + ">")));
            Console.WriteLine("Parameters:");
            foreach (var item in CliArgs)
                Console.WriteLine(item.Item1 + ": " + item.Item2);
            Console.WriteLine("Actual call's arguments were: " + string.Join(" ", args));
            return;
        }

        // Say hello
        Console.WriteLine("<<< Welcome to the RAWSimO Jenkins Handler >>>");

        // Echo the arguments passed
        Console.WriteLine("Starting RAWSimO wrapper with the following arguments:");
        for (var i = 0; i < CliArgs.Length; i++)
            Console.WriteLine(CliArgs[i].Item1 + ": " + args[i]);

        // Setup instance
        Console.Write("Initializing ... ");
        var seed = int.Parse(args[4]);
        var buildNumber = args[5];
        var logAction = (string message) => { Console.WriteLine(message); };
        var instance = InstanceIO.ReadInstance(args[0], args[1], args[2], logAction: logAction);
        instance.SettingConfig.LogAction = logAction;
        instance.SettingConfig.Seed = seed;
        instance.SettingConfig.StatisticsDirectory = Path.Combine(args[3], instance.Name + "-" + instance.SettingConfig.Name + "-" + instance.ControllerConfig.Name + "-" + instance.SettingConfig.Seed);
        instance.Randomizer = new RandomizerSimple(seed);
        Console.WriteLine("Done!");
        // Deus ex machina
        Console.WriteLine("Executing ... ");
        var before = DateTime.Now;
        SimulationExecutor.Execute(instance);
        var executionTime = DateTime.Now - before;
        Console.WriteLine("Simulation finished.");
        // Write short statistics to output
        instance.PrintStatistics((string s) => Console.WriteLine(s));
        // Log the evaluation statistics
        AppendStatLine(instance, seed, buildNumber, executionTime);
        // Finished
        Console.WriteLine(".Fin. - SUCCESS");
    }

    private static void AppendStatLine(Instance instance, int seed, string buildNumber, TimeSpan executionTime)
    {
        // Basic params
        var statFilePrefix = "stats";
        var statFileEnding = ".csv";
        var delimiter = ";";
        var plotDelimiter = ",";
        // Init file and write head, if not existing
        if (!File.Exists(statFilePrefix + statFileEnding))
            using (var sw = new StreamWriter(statFilePrefix + statFileEnding))
                sw.WriteLine(
                    "TimeStamp" + delimiter +
                    "TimeSpan" + delimiter +
                    "Instance" + delimiter +
                    "Setting" + delimiter +
                    "Config" + delimiter +
                    "Seed" + delimiter +
                    "BuildNumber" + delimiter +
                    "StatOverallBundlesHandled" + delimiter +
                    "StatOverallItemsHandled" + delimiter +
                    "StatOverallOrdersHandled" + delimiter +
                    "StatOverallItemsOrdered" + delimiter +
                    "StatOverallCollisions" + delimiter +
                    "StatOverallDistanceTraveled"
                );
        // Write evaluation results
        using (var sw = new StreamWriter(statFilePrefix + statFileEnding, true))
            sw.WriteLine(
                DateTime.Now.ToString(IOConstants.FORMATTER) + delimiter +
                executionTime.TotalSeconds.ToString(IOConstants.FORMATTER) + delimiter +
                instance.Name + delimiter +
                instance.ControllerConfig.Name + delimiter +
                instance.SettingConfig.Name + delimiter +
                seed.ToString(IOConstants.FORMATTER) + delimiter +
                buildNumber + delimiter +
                instance.StatOverallBundlesHandled.ToString(IOConstants.FORMATTER) + delimiter +
                instance.StatOverallItemsHandled.ToString(IOConstants.FORMATTER) + delimiter +
                instance.StatOverallOrdersHandled.ToString(IOConstants.FORMATTER) + delimiter +
                instance.StatOverallItemsOrdered.ToString(IOConstants.FORMATTER) + delimiter +
                instance.StatOverallCollisions.ToString(IOConstants.FORMATTER) + delimiter +
                instance.StatOverallDistanceTraveled.ToString(IOConstants.FORMATTER)
            );
        // Init instance specific stat-file for plotting
        var plotStatFile = statFilePrefix + "-" + instance.Name + "-" + instance.ControllerConfig.Name + statFileEnding;
        using (var sw = new StreamWriter(plotStatFile))
        {
            sw.WriteLine(
                "TimeSpan" + plotDelimiter +
                "BundlesHandled" + plotDelimiter +
                "OrdersHandled" + plotDelimiter +
                "Collisions" + plotDelimiter +
                "DistanceTraveled" + plotDelimiter +
                "OrderThroughputTime"
            );
            sw.WriteLine(
                executionTime.TotalSeconds.ToString(IOConstants.FORMATTER) + plotDelimiter +
                instance.StatOverallBundlesHandled.ToString(IOConstants.FORMATTER) + plotDelimiter +
                instance.StatOverallOrdersHandled.ToString(IOConstants.FORMATTER) + plotDelimiter +
                instance.StatOverallCollisions.ToString(IOConstants.FORMATTER) + plotDelimiter +
                instance.StatOverallDistanceTraveled.ToString(IOConstants.FORMATTER) + plotDelimiter +
                instance.StatOrderThroughputTimeAvg.ToString(IOConstants.FORMATTER)
            );
        }
        // ---> Init aggregated path planning plot file
        if (instance.Name.Contains("jenPP") && instance.SettingConfig.Name.Contains("jenPP") && instance.ControllerConfig.Name.Contains("jenPP"))
        {
            // --> Add aggregated path planning order stat file
            var aggregatedPathPlanningOrders = "stat-jenPP-aggregated-orders.csv";
            // Parse already existing file
            var lines = new List<string>();
            if (File.Exists(aggregatedPathPlanningOrders))
            {
                using (var sr = new StreamReader(aggregatedPathPlanningOrders))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }
            }
            // Add values of this run
            using (var sw = new StreamWriter(aggregatedPathPlanningOrders, false))
            {
                if (lines.Count > 0)
                {
                    sw.WriteLine(lines[0] + plotDelimiter + instance.Name + instance.ControllerConfig.Name);
                    sw.WriteLine(lines[1] + plotDelimiter + instance.StatOverallOrdersHandled.ToString(IOConstants.FORMATTER));
                }
                else
                {
                    sw.WriteLine(instance.Name + instance.ControllerConfig.Name);
                    sw.WriteLine(instance.StatOverallOrdersHandled.ToString(IOConstants.FORMATTER));
                }
            }
            // --> Add aggregated path planning bundle stat file
            var aggregatedPathPlanningBundles = "stat-jenPP-aggregated-bundles.csv";
            // Parse already existing file
            lines = [];
            if (File.Exists(aggregatedPathPlanningBundles))
            {
                using (var sr = new StreamReader(aggregatedPathPlanningBundles))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }
            }
            // Add values of this run
            using (var sw = new StreamWriter(aggregatedPathPlanningBundles, false))
            {
                if (lines.Count > 0)
                {
                    sw.WriteLine(lines[0] + plotDelimiter + instance.Name + instance.ControllerConfig.Name);
                    sw.WriteLine(lines[1] + plotDelimiter + instance.StatOverallBundlesHandled.ToString(IOConstants.FORMATTER));
                }
                else
                {
                    sw.WriteLine(instance.Name + instance.ControllerConfig.Name);
                    sw.WriteLine(instance.StatOverallBundlesHandled.ToString(IOConstants.FORMATTER));
                }
            }
            // --> Add aggregated path planning order stat file
            var aggregatedPathPlanningDistanceTraveled = "stat-jenPP-aggregated-distance.csv";
            // Parse already existing file
            lines = [];
            if (File.Exists(aggregatedPathPlanningDistanceTraveled))
            {
                using (var sr = new StreamReader(aggregatedPathPlanningDistanceTraveled))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }
            }
            // Add values of this run
            using (var sw = new StreamWriter(aggregatedPathPlanningDistanceTraveled, false))
            {
                if (lines.Count > 0)
                {
                    sw.WriteLine(lines[0] + plotDelimiter + instance.Name + instance.ControllerConfig.Name);
                    sw.WriteLine(lines[1] + plotDelimiter + instance.StatOverallDistanceTraveled.ToString(IOConstants.FORMATTER));
                }
                else
                {
                    sw.WriteLine(instance.Name + instance.ControllerConfig.Name);
                    sw.WriteLine(instance.StatOverallDistanceTraveled.ToString(IOConstants.FORMATTER));
                }
            }
            // --> Add aggregated path planning time stat file
            var aggregatedPathPlanningTime = "stat-jenPP-aggregated-time.csv";
            // Parse already existing file
            lines = [];
            if (File.Exists(aggregatedPathPlanningTime))
            {
                using (var sr = new StreamReader(aggregatedPathPlanningTime))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }
            }
            // Add values of this run
            using (var sw = new StreamWriter(aggregatedPathPlanningTime, false))
            {
                if (lines.Count > 0)
                {
                    sw.WriteLine(lines[0] + plotDelimiter + instance.Name + instance.ControllerConfig.Name);
                    sw.WriteLine(lines[1] + plotDelimiter + executionTime.TotalSeconds.ToString(IOConstants.FORMATTER));
                }
                else
                {
                    sw.WriteLine(instance.Name + instance.ControllerConfig.Name);
                    sw.WriteLine(executionTime.TotalSeconds.ToString(IOConstants.FORMATTER));
                }
            }
        }
        // ---> Init aggregated jenkins plot file
        if (instance.Name.Contains("jenkins") && instance.SettingConfig.Name.Contains("jenkins") && instance.ControllerConfig.Name.Contains("jenkins"))
        {
            // --> Add aggregated throughput stat file
            var aggregatedThroughputStatFile = "stat-jenkins-aggregated-throughput.csv";
            // Parse already existing file
            var lines = new List<string>();
            if (File.Exists(aggregatedThroughputStatFile))
            {
                using (var sr = new StreamReader(aggregatedThroughputStatFile))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }
            }
            // Add values of this run
            using (var sw = new StreamWriter(aggregatedThroughputStatFile, false))
            {
                if (lines.Count > 0)
                {
                    sw.WriteLine(lines[0] + plotDelimiter + instance.Name + instance.ControllerConfig.Name);
                    sw.WriteLine(lines[1] + plotDelimiter + instance.StatOrderThroughputTimeAvg.ToString(IOConstants.FORMATTER));
                }
                else
                {
                    sw.WriteLine(instance.Name + instance.ControllerConfig.Name);
                    sw.WriteLine(instance.StatOrderThroughputTimeAvg.ToString(IOConstants.FORMATTER));
                }
            }
            // --> Add aggregated orders handled stat file
            var aggregatedOrdersHandledStatFile = "stat-jenkins-aggregated-orders.csv";
            // Parse already existing file
            lines = [];
            if (File.Exists(aggregatedOrdersHandledStatFile))
            {
                using (var sr = new StreamReader(aggregatedOrdersHandledStatFile))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }
            }
            // Add values of this run
            using (var sw = new StreamWriter(aggregatedOrdersHandledStatFile, false))
            {
                if (lines.Count > 0)
                {
                    sw.WriteLine(lines[0] + plotDelimiter + instance.Name + instance.ControllerConfig.Name);
                    sw.WriteLine(lines[1] + plotDelimiter + instance.StatOverallOrdersHandled);
                }
                else
                {
                    sw.WriteLine(instance.Name + instance.ControllerConfig.Name);
                    sw.WriteLine(instance.StatOverallOrdersHandled.ToString());
                }
            }
            // --> Add aggregated bundles handled stat file
            var aggregatedBundlesHandledStatFile = "stat-jenkins-aggregated-bundles.csv";
            // Parse already existing file
            lines = [];
            if (File.Exists(aggregatedBundlesHandledStatFile))
            {
                using (var sr = new StreamReader(aggregatedBundlesHandledStatFile))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }
            }
            // Add values of this run
            using (var sw = new StreamWriter(aggregatedBundlesHandledStatFile, false))
            {
                if (lines.Count > 0)
                {
                    sw.WriteLine(lines[0] + plotDelimiter + instance.Name + instance.ControllerConfig.Name);
                    sw.WriteLine(lines[1] + plotDelimiter + instance.StatOverallBundlesHandled);
                }
                else
                {
                    sw.WriteLine(instance.Name + instance.ControllerConfig.Name);
                    sw.WriteLine(instance.StatOverallBundlesHandled.ToString());
                }
            }
            // --> Add aggregated time stat file
            var aggregatedTimeStatFile = "stat-jenkins-aggregated-time.csv";
            // Parse already existing file
            lines = [];
            if (File.Exists(aggregatedTimeStatFile))
            {
                using (var sr = new StreamReader(aggregatedTimeStatFile))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }
            }
            // Add values of this run
            using (var sw = new StreamWriter(aggregatedTimeStatFile, false))
            {
                if (lines.Count > 0)
                {
                    sw.WriteLine(lines[0] + plotDelimiter + instance.Name + instance.ControllerConfig.Name);
                    sw.WriteLine(lines[1] + plotDelimiter + executionTime.TotalSeconds);
                }
                else
                {
                    sw.WriteLine(instance.Name + instance.ControllerConfig.Name);
                    sw.WriteLine(executionTime.TotalSeconds.ToString());
                }
            }
        }
    }
}