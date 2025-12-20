using RAWSimO.Rendering2D;

namespace RAWSimO.WebServer.SimulationHost.Models;

public sealed record RenderFrameDto(
    int ViewportWidthPx,
    int ViewportHeightPx,
    int TierIndex,
    double SimTime,
    IReadOnlyList<RenderCommandDto> Commands
)
{
    public static RenderFrameDto Empty(int w, int h, int tierIndex) =>
        new(w, h, tierIndex, 0, Array.Empty<RenderCommandDto>());

    public static RenderFrameDto From(RenderFrame frame, int w, int h, int tierIndex, double simTime)
        => new(w, h, tierIndex, simTime, frame.Commands.Select(RenderCommandDto.From).ToArray());
}
