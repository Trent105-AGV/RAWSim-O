using RAWSimO.Rendering2D;

namespace RAWSimO.WebServer.SimulationHost.Models;

public sealed record RenderCommandDto(string Kind, object Payload)
{
    public static RenderCommandDto From(RenderCommand command) => command switch
    {
        FillRect c => new RenderCommandDto("FillRect", new FillRectPayload(c.Rect, c.Color)),
        StrokeRect c => new RenderCommandDto("StrokeRect", new StrokeRectPayload(c.Rect, c.Color, c.Thickness)),
        FillCircle c => new RenderCommandDto("FillCircle", new FillCirclePayload(c.Center, c.Radius, c.Color)),
        StrokeCircle c => new RenderCommandDto("StrokeCircle", new StrokeCirclePayload(c.Center, c.Radius, c.Color, c.Thickness)),
        Line c => new RenderCommandDto("Line", new LinePayload(c.From, c.To, c.Color, c.Thickness)),
        _ => new RenderCommandDto("Unknown", new { })
    };

    public sealed record FillRectPayload(RectF Rect, RenderColor Color);

    public sealed record StrokeRectPayload(RectF Rect, RenderColor Color, float Thickness);

    public sealed record FillCirclePayload(PointF Center, float Radius, RenderColor Color);

    public sealed record StrokeCirclePayload(PointF Center, float Radius, RenderColor Color, float Thickness);

    public sealed record LinePayload(PointF StartPoint, PointF To, RenderColor Color, float Thickness);
}
