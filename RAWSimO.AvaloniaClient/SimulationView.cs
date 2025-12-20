using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using RAWSimO.Core;
using RAWSimO.Rendering2D;

namespace RAWSimO.AvaloniaClient;

public sealed class SimulationView : Control
{
    private readonly Simulation2DCommandBuilder _builder = new();

    public Instance Instance { get; set; }

    public Rendering2D.RenderOptions Options { get; set; }

    private readonly Dictionary<RenderColor, IBrush> _brushCache = new();

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (Bounds.Width <= 1 || Bounds.Height <= 1)
            return;

        var instance = Instance;
        var viewport = new RectF(0, 0, (float)Bounds.Width, (float)Bounds.Height);

        if (instance == null)
        {
            context.FillRectangle(Brushes.White, new Rect(0, 0, Bounds.Width, Bounds.Height));
            return;
        }

        RenderFrame frame;
        try
        {
            frame = _builder.Build(instance, tierIndex: 0, viewport: viewport, options: Options);
        }
        catch
        {
            context.FillRectangle(Brushes.White, new Rect(0, 0, Bounds.Width, Bounds.Height));
            return;
        }

        foreach (var cmd in frame.Commands)
        {
            switch (cmd)
            {
                case FillRect r:
                    context.FillRectangle(GetBrush(r.Color), ToRect(r.Rect));
                    break;

                case StrokeRect r:
                    context.DrawRectangle(new Pen(GetBrush(r.Color), Math.Max(1, r.Thickness)), ToRect(r.Rect));
                    break;

                case FillCircle c:
                    context.DrawEllipse(GetBrush(c.Color), null, ToRect(CircleToRect(c.Center, c.Radius)));
                    break;

                case StrokeCircle c:
                    context.DrawEllipse(null, new Pen(GetBrush(c.Color), Math.Max(1, c.Thickness)),
                        ToRect(CircleToRect(c.Center, c.Radius)));
                    break;

                case Line l:
                    context.DrawLine(new Pen(GetBrush(l.Color), Math.Max(1, l.Thickness)), ToPoint(l.From),
                        ToPoint(l.To));
                    break;
            }
        }
    }

    private IBrush GetBrush(RenderColor color)
    {
        if (_brushCache.TryGetValue(color, out var brush))
            return brush;

        brush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
        _brushCache.Add(color, brush);
        return brush;
    }

    private static Rect ToRect(RectF r) => new(r.X, r.Y, r.Width, r.Height);

    private static Point ToPoint(PointF p) => new(p.X, p.Y);

    private static RectF CircleToRect(PointF center, float radius)
        => new(center.X - radius, center.Y - radius, radius * 2, radius * 2);
}
