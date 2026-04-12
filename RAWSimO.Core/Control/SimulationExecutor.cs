namespace RAWSimO.Core.Control;

using System.Diagnostics;

/// <summary>
/// Used to execute simulation instances.
/// </summary>
public class SimulationExecutor
{
    /// <summary>
    /// Executes the given simulation.
    /// </summary>
    /// <param name="instance">The instance to execute including the configuration to use.</param>
    public static void Execute(Instance instance)
    {
        // Set basic stuff
        var warmup_time = instance.SettingConfig.SimulationWarmupTime;
        var simulation_time = instance.SettingConfig.SimulationDuration;
        // Execute
        instance.LogDefault(">>> Warming up ...");
        instance.StartExecutionTiming();

        var warmupStopwatch = instance.SettingConfig.EnablePerformanceProfiling ? Stopwatch.StartNew() : null;
        instance.Controller.Update(warmup_time);
        if (warmupStopwatch != null)
        {
            warmupStopwatch.Stop();
            instance.LogDefault($">>> Warmup CPU elapsed: {warmupStopwatch.Elapsed.TotalSeconds:0.###}s");
        }

        instance.LogDefault(">>> Warmup finished - starting simulation ...");
        instance.StatReset();

        var simulationStopwatch = instance.SettingConfig.EnablePerformanceProfiling ? Stopwatch.StartNew() : null;
        instance.Controller.Update(simulation_time);
        if (simulationStopwatch != null)
        {
            simulationStopwatch.Stop();
            instance.LogDefault($">>> Runtime CPU elapsed: {simulationStopwatch.Elapsed.TotalSeconds:0.###}s");
        }

        instance.StopExecutionTiming();
        instance.LogDefault(">>> Simulation finished - writing results ...");
        // Print results
        instance.WriteStatistics();
        instance.LogDefault(">>> Results written");
    }
}