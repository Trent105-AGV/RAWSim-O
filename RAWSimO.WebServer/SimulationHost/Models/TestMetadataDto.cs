namespace RAWSimO.WebServer.SimulationHost.Models;

public sealed record BotStateInfo(
    int Id,
    double X,
    double Y,
    double Radius,
    double Orientation,
    string State,
    bool IsIdle,
    bool HasPod,
    string? TaskType
);

public sealed record TestMetadataDto(
    bool SimulationRunning,
    double SimTime,
    int AgvCount,
    int InputStationCount,
    int OutputStationCount,
    int PodCount,
    int WaypointCount,
    int PendingOrderCount,
    int OpenOrderCount,
    int CompletedOrderCount,
    int CompletedBundleCount,
    int IdleBotCount,
    int BusyBotCount,
    IReadOnlyList<BotStateInfo> BotStates,
    string? Error,
    bool HealthOk
)
{
    public static TestMetadataDto Empty()
    {
        return new TestMetadataDto(
            SimulationRunning: false,
            SimTime: 0,
            AgvCount: 0,
            InputStationCount: 0,
            OutputStationCount: 0,
            PodCount: 0,
            WaypointCount: 0,
            PendingOrderCount: 0,
            OpenOrderCount: 0,
            CompletedOrderCount: 0,
            CompletedBundleCount: 0,
            IdleBotCount: 0,
            BusyBotCount: 0,
            BotStates: [],
            Error: null,
            HealthOk: true
        );
    }
}
