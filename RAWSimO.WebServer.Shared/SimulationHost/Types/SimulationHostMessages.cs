using MessagePack;

namespace RAWSimO.WebServer.Shared.SimulationHost.Types;

#region Enums

public enum ESimulationStartResult
{
    Success = 0,
    AlreadyRunning = 1,
    InvalidConfiguration = 2,
    UnknownError = 999
}

#endregion

#region DTOs

[MessagePackObject]
public sealed record StartRequest(
    [property: Key(0)] string Instance,
    [property: Key(1)] string Setting,
    [property: Key(2)] string ControlConfig,
    [property: Key(3)] int? Seed,
    [property: Key(4)] string? Tag,
    [property: Key(5)] byte[]? ResourceZip,
    [property: Key(6)] int WidthPx = 800,
    [property: Key(7)] int HeightPx = 600
);

[MessagePackObject]
public sealed record AppendTaskPositionRequest(
    [property: Key(0)] int ItemDescriptionId,
    [property: Key(1)] int Count
);

[MessagePackObject]
public sealed record AppendTaskRequest(
    [property: Key(0)] double? TimeStamp,
    [property: Key(1)] IReadOnlyList<AppendTaskPositionRequest> Positions,
    [property: Key(2)] int? TargetOutputStationId = null
);

[MessagePackObject]
public sealed record AppendTasksRequest(
    [property: Key(0)] IReadOnlyList<AppendTaskRequest> Tasks
);

[MessagePackObject]
public sealed record AppendTasksResponse(
    [property: Key(0)] bool Accepted,
    [property: Key(1)] int AppendedCount,
    [property: Key(2)] int PendingOrderCount = 0,
    [property: Key(3)] int OpenOrderCount = 0,
    [property: Key(4)] int CompletedOrderCount = 0,
    [property: Key(5)] string? Error = null
);

[MessagePackObject]
public sealed record EndSimulationResponse(
    [property: Key(0)] string? OutputDirName,
    [property: Key(1)] double SimTime,
    [property: Key(2)] string? Error
);

[MessagePackObject]
public sealed record DownloadStatisticsRequest(
    [property: Key(0)] string OutputDirName
);

[MessagePackObject]
public sealed record DownloadStatisticsResponse(
    [property: Key(0)] string OutputDirName,
    [property: Key(1)] string FileName,
    [property: Key(2)] byte[] ZipBytes
);

[MessagePackObject]
public sealed record StatusResponse(
    [property: Key(0)] bool Running,
    [property: Key(1)] double SimTime,
    [property: Key(2)] string? Error,
    // Running instance configuration snapshot (resource zip intentionally excluded)
    [property: Key(3)] string? Instance = null,
    [property: Key(4)] string? Setting = null,
    [property: Key(5)] string? ControlConfig = null,
    [property: Key(6)] int? Seed = null,
    [property: Key(7)] string? Tag = null,
    [property: Key(8)] IReadOnlyList<ItemDescriptionOption>? AvailableItemDescriptions = null
);

[MessagePackObject]
public sealed record ItemDescriptionOption(
    [property: Key(0)] int Id,
    [property: Key(1)] string Description
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
public sealed record UpdateRenderOptionsRequest(
    [property: Key(0)] bool DrawBots,
    [property: Key(1)] bool DrawPods,
    [property: Key(2)] bool DrawStations,
    [property: Key(3)] bool DrawWaypoints,
    [property: Key(4)] int? TierIndex = null
);

[MessagePackObject]
public sealed record UpdateRenderOptionsResponse(
    [property: Key(0)] bool Accepted,
    [property: Key(1)] bool DrawBots,
    [property: Key(2)] bool DrawPods,
    [property: Key(3)] bool DrawStations,
    [property: Key(4)] bool DrawWaypoints,
    [property: Key(5)] int TierIndex,
    [property: Key(6)] string? Error = null
);

[MessagePackObject]
public sealed record StartResponse(
    [property: Key(0)] ESimulationStartResult Result,
    [property: Key(1)] string? Error = null
);

#endregion
