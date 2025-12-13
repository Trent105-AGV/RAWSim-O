using System;

namespace RAWSimO.Rendering2D;

public readonly record struct PointF(float X, float Y);

public readonly record struct RectF(float X, float Y, float Width, float Height)
{
    public float Left => X;
    public float Top => Y;
    public float Right => X + Width;
    public float Bottom => Y + Height;
}

public sealed class ViewportTransform
{
    private readonly double _worldWidth;
    private readonly double _worldHeight;
    private readonly RectF _viewport;
    private readonly float _scale;
    private readonly float _offsetX;
    private readonly float _offsetY;

    public ViewportTransform(double worldWidth, double worldHeight, RectF viewport, float paddingPx)
    {
        _worldWidth = worldWidth;
        _worldHeight = worldHeight;
        _viewport = viewport;

        var availableW = Math.Max(1f, viewport.Width - 2 * paddingPx);
        var availableH = Math.Max(1f, viewport.Height - 2 * paddingPx);

        var sx = (float)(availableW / Math.Max(1e-9, worldWidth));
        var sy = (float)(availableH / Math.Max(1e-9, worldHeight));
        _scale = MathF.Min(sx, sy);

        var contentW = (float)(worldWidth * _scale);
        var contentH = (float)(worldHeight * _scale);

        _offsetX = viewport.X + (viewport.Width - contentW) / 2f;
        _offsetY = viewport.Y + (viewport.Height - contentH) / 2f;
    }

    public PointF WorldToScreen(double x, double y)
    {
        // Y axis is inverted compared to typical screen coordinates.
        var px = _offsetX + (float)(x * _scale);
        var py = _offsetY + (float)((_worldHeight - y) * _scale);
        return new PointF(px, py);
    }

    public float WorldToScreenLength(double length) => (float)(length * _scale);

    public RectF WorldToScreenRect(double x, double y, double w, double h)
    {
        var tl = WorldToScreen(x, y + h);
        var br = WorldToScreen(x + w, y);
        return new RectF(tl.X, tl.Y, br.X - tl.X, br.Y - tl.Y);
    }
}
