using System.Collections.Generic;

namespace RAWSimO.Rendering2D;

public readonly record struct RenderColor(byte R, byte G, byte B, byte A = 255)
{
    public static RenderColor White => new(255, 255, 255);
    public static RenderColor DarkGray => new(30, 30, 30);
}

public abstract record RenderCommand;

public sealed record RenderFrame(IReadOnlyList<RenderCommand> Commands);

public sealed record FillRect(RectF Rect, RenderColor Color) : RenderCommand;

public sealed record StrokeRect(RectF Rect, RenderColor Color, float Thickness) : RenderCommand;

public sealed record FillCircle(PointF Center, float Radius, RenderColor Color) : RenderCommand;

public sealed record StrokeCircle(PointF Center, float Radius, RenderColor Color, float Thickness) : RenderCommand;

public sealed record Line(PointF From, PointF To, RenderColor Color, float Thickness) : RenderCommand;
