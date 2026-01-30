using MessagePack;

namespace RAWSimO.WebServer.Shared.Hubs;

[MessagePackObject]
public sealed record StartSimulationNotification(
    [property: Key(0)] string Instance,
    [property: Key(1)] string Setting,
    [property: Key(2)] string ControlConfig,
    [property: Key(3)] int? Seed,
    [property: Key(4)] string? Tag,
    [property: Key(5)] DateTimeOffset StartedAtUtc
);

[MessagePackObject]
public sealed record PauseSimulationNotification(
    [property: Key(0)] double SimTime,
    [property: Key(1)] DateTimeOffset PausedAtUtc
);

[MessagePackObject]
public sealed record ResumeSimulationNotification(
    [property: Key(0)] double SimTime,
    [property: Key(1)] DateTimeOffset ResumedAtUtc
);

[MessagePackObject]
public sealed record EndSimulationNotification(
    [property: Key(0)] double SimTime,
    [property: Key(1)] string? Error,
    [property: Key(2)] DateTimeOffset EndedAtUtc
);

public interface IMessageClient
{
    /// <summary>
    /// Called when a simulation is started
    /// </summary>
    Task StartSimulation(StartSimulationNotification notification);

    /// <summary>
    /// Called when a simulation is paused
    /// </summary>
    Task PauseSimulation(PauseSimulationNotification notification);

    /// <summary>
    /// Called when a simulation is resumed
    /// </summary>
    Task ResumeSimulation(ResumeSimulationNotification notification);

    /// <summary>
    /// Called when a simulation is ended
    /// </summary>
    Task EndSimulation(EndSimulationNotification notification);
}