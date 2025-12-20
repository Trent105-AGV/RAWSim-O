using MessagePack;

namespace RAWSimO.WebServer.Shared.SimulationHost.Types;

#region Enums

public enum ESimulationStartResult
{
    Success = 0,
    AlreadyRunning = 1,
    InstanceNotFound = 2,
    InvalidConfiguration = 3,
    UnknownError = 999
}

#endregion

#region DTOs

[MessagePackObject]
public sealed record StartRequest(
    [property: Key(0)] string Instance,
    [property: Key(1)] string Setting,
    [property: Key(2)] string ControlConfig,
    [property: Key(3)] string StatisticsDir,
    [property: Key(4)] int? Seed,
    [property: Key(5)] string? Tag
);

[MessagePackObject]
public sealed record StatusResponse(
    [property: Key(0)] bool Running,
    [property: Key(1)] double SimTime,
    [property: Key(2)] string? Error
);

[MessagePackObject]
public sealed record RenderFrameRequest(
    [property: Key(0)] int WidthPx = 800,
    [property: Key(1)] int HeightPx = 600,
    [property: Key(2)] int TierIndex = 0,
    [property: Key(3)] bool DrawBots = true,
    [property: Key(4)] bool DrawPods = true,
    [property: Key(5)] bool DrawStations = true,
    [property: Key(6)] bool DrawWaypoints = false
);

[MessagePackObject]
public sealed record RenderFrameResponse(
    [property: Key(0)] int ViewportWidthPx,
    [property: Key(1)] int ViewportHeightPx,
    [property: Key(2)] int TierIndex,
    [property: Key(3)] double SimTime,
    [property: Key(4)] byte[]? FrameData
);

[MessagePackObject]
public sealed record StartResponse(
    [property: Key(0)] ESimulationStartResult Result,
    [property: Key(1)] string? Error = null
);

#endregion
