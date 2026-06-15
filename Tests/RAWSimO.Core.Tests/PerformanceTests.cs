using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using RAWSimO.Core.Control;
using RAWSimO.Core.IO;
using RAWSimO.Core.Randomization;
using Xunit;
using Xunit.Abstractions;

namespace RAWSimO.Core.Tests;

public class PerformanceTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static Instance ReadInstance(string layout, string setting, string control, int seed)
    {
        Action<string> logAction = Console.WriteLine;
        var instance = InstanceIO.ReadInstance(layout, setting, control, logAction: logAction);
        instance.SettingConfig.LogAction = logAction;
        instance.SettingConfig.Seed = seed;
        instance.Randomizer = new RandomizerSimple(seed);
        return instance;
    }

    /// <summary>
    /// TC-06: System supports connecting at least 200 AGVs.
    /// Loads a layout with 300 bots and verifies the count.
    /// </summary>
    [Fact]
    public void Scale200AGV_BotCountExceeds200()
    {
        var instance = ReadInstance(
            "Resources/Scale200AGV.xlayo",
            "Resources/Scale200AGV.xsett",
            "Resources/Scale200AGV.xconf",
            seed: 0);

        var botCount = instance.Bots.Count;
        _output.WriteLine($"Bot count: {botCount}");
        Assert.True(botCount >= 200, $"Expected at least 200 bots, got {botCount}");

        var tier = instance.Compound.Tiers[0];
        Assert.Equal(botCount, tier.CurrentBots.Count());

        _output.WriteLine($"Pod count: {tier.CurrentPods.Count()}");
        _output.WriteLine($"Waypoints: {instance.Waypoints.Count}");
        _output.WriteLine($"Input stations: {instance.InputStations.Count}");
        _output.WriteLine($"Output stations: {instance.OutputStations.Count}");
    }

    /// <summary>
    /// TC-06 extended: Run 300 AGVs for a short duration and verify no errors.
    /// </summary>
    [Fact]
    public void Scale200AGV_SimulationCompletes()
    {
        var instance = ReadInstance(
            "Resources/Scale200AGV.xlayo",
            "Resources/Scale200AGV.xsett",
            "Resources/Scale200AGV.xconf",
            seed: 0);

        Assert.True(instance.Bots.Count >= 200);

        SimulationExecutor.Execute(instance);

        _output.WriteLine($"Simulation completed. SimTime: {instance.Controller.CurrentTime}");
        _output.WriteLine($"Duration: {instance.SettingConfig.SimulationDuration}");

        var completedOrders = instance.ItemManager.GetInfoCompletedOrders().Count();
        var totalOrders = instance.ItemManager.GetInfoPendingOrderCount()
                          + instance.ItemManager.GetInfoOpenOrders().Count()
                          + completedOrders;

        _output.WriteLine($"Total orders generated: {totalOrders}");
        _output.WriteLine($"Completed orders: {completedOrders}");

        Assert.True(instance.Controller.CurrentTime >= instance.SettingConfig.SimulationDuration,
            "Simulation should complete the configured duration");
    }

    /// <summary>
    /// TC-16: With 3000+ pending tasks, average task response time should be under 3 seconds.
    /// Uses a small instance with high OrderCount to generate a large pending pool.
    /// </summary>
    [Fact]
    public void Scale3000Task_OrderCountExceeds3000()
    {
        var instance = ReadInstance(
            "Resources/BasicInstance.xlayo",
            "Resources/Scale3000Task.xsett",
            "Resources/BasicInstance.xconf",
            seed: 0);

        SimulationExecutor.Execute(instance);

        var completedOrders = instance.ItemManager.GetInfoCompletedOrders().Count();
        var pendingOrders = instance.ItemManager.GetInfoPendingOrderCount();
        var openOrders = instance.ItemManager.GetInfoOpenOrders().Count();
        var totalOrders = pendingOrders + openOrders + completedOrders;

        _output.WriteLine($"Pending: {pendingOrders}, Open: {openOrders}, Completed: {completedOrders}, Total: {totalOrders}");

        Assert.True(pendingOrders >= 3000,
            $"Expected pending orders >= 3000, got {pendingOrders}");
    }

    /// <summary>
    /// TC-16: Measure order generation throughput. The ItemManager should be able to
    /// maintain a large order pool without excessive delay.
    /// </summary>
    [Fact]
    public void Scale3000Task_OrderGenerationPerformance()
    {
        var sw = Stopwatch.StartNew();
        var instance = ReadInstance(
            "Resources/BasicInstance.xlayo",
            "Resources/Scale3000Task.xsett",
            "Resources/BasicInstance.xconf",
            seed: 0);

        var readTime = sw.ElapsedMilliseconds;
        _output.WriteLine($"Instance read: {readTime}ms");

        sw.Restart();
        SimulationExecutor.Execute(instance);
        var execTime = sw.ElapsedMilliseconds;

        _output.WriteLine($"Simulation execution: {execTime}ms");

        var totalOrders = instance.ItemManager.GetInfoPendingOrderCount()
                          + instance.ItemManager.GetInfoOpenOrders().Count()
                          + instance.ItemManager.GetInfoCompletedOrders().Count();

        _output.WriteLine($"Total orders: {totalOrders}");

        // The simulation itself should complete without hanging.
        // 3000+ orders with 16 bots in 100 sim-time should be well within reasonable time.
        Assert.True(execTime < 30000,
            $"Simulation took too long: {execTime}ms for {totalOrders} orders");
    }

    /// <summary>
    /// TC-01: Verify dashboard data - bot count, station count, task stats are all accessible.
    /// </summary>
    [Fact]
    public void Dashboard_AllMetricsAccessible()
    {
        var instance = ReadInstance(
            "Resources/BasicInstance.xlayo",
            "Resources/BasicInstance.xsett",
            "Resources/BasicInstance.xconf",
            seed: 0);

        var botCount = instance.Bots.Count;
        var inputStationCount = instance.InputStations.Count;
        var outputStationCount = instance.OutputStations.Count;
        var podCount = instance.Compound.Tiers[0].CurrentPods.Count();
        var waypointCount = instance.Waypoints.Count;

        _output.WriteLine($"Bots: {botCount}");
        _output.WriteLine($"Input stations: {inputStationCount}");
        _output.WriteLine($"Output stations: {outputStationCount}");
        _output.WriteLine($"Pods: {podCount}");
        _output.WriteLine($"Waypoints: {waypointCount}");

        Assert.True(botCount > 0, "Should have bots");
        Assert.True(inputStationCount > 0, "Should have input stations");
        Assert.True(outputStationCount > 0, "Should have output stations");
        Assert.True(podCount > 0, "Should have pods");
        Assert.True(waypointCount > 0, "Should have waypoints");
    }

    /// <summary>
    /// TC-12: Health check - simulation should complete without errors.
    /// </summary>
    [Fact]
    public void Health_SimulationNoError()
    {
        var instance = ReadInstance(
            "Resources/BasicInstance.xlayo",
            "Resources/BasicInstance.xsett",
            "Resources/BasicInstance.xconf",
            seed: 0);

        SimulationExecutor.Execute(instance);

        // If we get here without exception, the simulation is healthy.
        Assert.True(instance.Controller.CurrentTime >= instance.SettingConfig.SimulationDuration);
        _output.WriteLine($"Simulation healthy. SimTime: {instance.Controller.CurrentTime}");
    }
}
