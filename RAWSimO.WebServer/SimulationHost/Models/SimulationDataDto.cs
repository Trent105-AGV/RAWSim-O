namespace RAWSimO.WebServer.SimulationHost.Models;

public sealed record SimulationCircleDto(
    int Id,
    double X,
    double Y,
    double Radius
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
    IReadOnlyList<SimulationCircleDto> Bots,
    IReadOnlyList<SimulationCircleDto> Pods,
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
            Bots: [],
            Pods: [],
            InputStations: [],
            OutputStations: [],
            Waypoints: []);
    }
}
