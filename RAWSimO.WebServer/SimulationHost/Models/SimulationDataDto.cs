namespace RAWSimO.WebServer.SimulationHost.Models;

public sealed record SimulationCircleDto(
    int Id,
    double X,
    double Y,
    double Radius,
    double Orientation = 0
);

public sealed record SimulationPodItemDto(
    int ItemDescriptionId,
    int Count
);

public sealed record SimulationPodDto(
    int Id,
    double X,
    double Y,
    double Radius,
    IReadOnlyList<SimulationPodItemDto> Contents
);

public sealed record SimulationPointDto(
    int Id,
    double X,
    double Y
);

public sealed record SimulationDataDto(
    int TierIndex,
    double SimTime,
    double WorldWidth,
    double WorldHeight,
    int PendingOrderCount,
    int OpenOrderCount,
    int CompletedOrderCount,
    IReadOnlyList<SimulationCircleDto> Bots,
    IReadOnlyList<SimulationPodDto> Pods,
    IReadOnlyList<SimulationCircleDto> InputStations,
    IReadOnlyList<SimulationCircleDto> OutputStations,
    IReadOnlyList<SimulationPointDto> Waypoints
)
{
    public static SimulationDataDto Empty(int tierIndex, double worldWidth, double worldHeight)
    {
        return new SimulationDataDto(
            TierIndex: tierIndex,
            SimTime: 0,
            WorldWidth: worldWidth,
            WorldHeight: worldHeight,
            PendingOrderCount: 0,
            OpenOrderCount: 0,
            CompletedOrderCount: 0,
            Bots: [],
            Pods: [],
            InputStations: [],
            OutputStations: [],
            Waypoints: []);
    }
}
