using System;
using System.Collections.Generic;
using System.Linq;
using RAWSimO.Core.Info;

namespace RAWSimO.Rendering2D;

public sealed class Simulation2DCommandBuilder
{
    public RenderFrame Build(IInstanceInfo instance, int tierIndex, RectF viewport, RenderOptions options = null)
    {
        options ??= new RenderOptions();

        var tiers = instance.GetInfoTiers().ToList();
        if (tiers.Count == 0)
            return new RenderFrame([]);

        if (tierIndex < 0) tierIndex = 0;
        if (tierIndex >= tiers.Count) tierIndex = tiers.Count - 1;

        var tier = tiers[tierIndex];
        var worldW = tier.GetInfoLength();
        var worldH = tier.GetInfoWidth();
        var transform = new ViewportTransform(worldW, worldH, viewport, options.PaddingPx);

        var commands = new List<RenderCommand>(capacity: 256)
        {
            // Background
            new FillRect(viewport, RenderColor.White),
            // Border
            new StrokeRect(transform.WorldToScreenRect(0, 0, worldW, worldH), RenderColor.DarkGray,
                Thickness: 1)
        };

        if (options.DrawStations)
            AddStations(commands, tier, transform);

        if (options.DrawPods)
            AddPods(commands, tier, transform);

        if (options.DrawBots)
            AddBots(commands, tier, transform);

        if (options.DrawWaypoints)
            AddWaypoints(commands, tier, transform);

        return new RenderFrame(commands);
    }

    private static void AddStations(List<RenderCommand> commands, ITierInfo tier, ViewportTransform transform)
    {
        foreach (var s in tier.GetInfoInputStations())
        {
            var rect = transform.WorldToScreenRect(s.GetInfoTLX(), s.GetInfoTLY(), s.GetInfoLength(), s.GetInfoWidth());
            commands.Add(new FillRect(rect, new RenderColor(200, 230, 255)));
            commands.Add(new StrokeRect(rect, RenderColor.DarkGray, Thickness: 1));
        }

        foreach (var s in tier.GetInfoOutputStations())
        {
            var rect = transform.WorldToScreenRect(s.GetInfoTLX(), s.GetInfoTLY(), s.GetInfoLength(), s.GetInfoWidth());
            commands.Add(new FillRect(rect, new RenderColor(220, 255, 220)));
            commands.Add(new StrokeRect(rect, RenderColor.DarkGray, Thickness: 1));
        }
    }

    private static void AddBots(List<RenderCommand> commands, ITierInfo tier, ViewportTransform transform)
    {
        foreach (var b in tier.GetInfoBots())
        {
            var center = transform.WorldToScreen(b.GetInfoCenterX(), b.GetInfoCenterY());
            var r = MathF.Max(2, transform.WorldToScreenLength(b.GetInfoRadius()));

            commands.Add(new FillCircle(center, r, new RenderColor(60, 120, 255)));
            commands.Add(new StrokeCircle(center, r, RenderColor.DarkGray, Thickness: 1));

            var headingLen = r * 1.2f;
            var angle = (float)b.GetInfoOrientation();
            var dx = MathF.Cos(angle) * headingLen;
            var dy = -MathF.Sin(angle) * headingLen;
            commands.Add(new Line(center, new PointF(center.X + dx, center.Y + dy), RenderColor.DarkGray,
                Thickness: 1));
        }
    }

    private static void AddPods(List<RenderCommand> commands, ITierInfo tier, ViewportTransform transform)
    {
        foreach (var p in tier.GetInfoPods())
        {
            var center = transform.WorldToScreen(p.GetInfoCenterX(), p.GetInfoCenterY());
            var r = MathF.Max(2, transform.WorldToScreenLength(p.GetInfoRadius()));

            commands.Add(new FillCircle(center, r, new RenderColor(255, 190, 80)));
            commands.Add(new StrokeCircle(center, r, RenderColor.DarkGray, Thickness: 1));
        }
    }

    private static void AddWaypoints(List<RenderCommand> commands, ITierInfo tier, ViewportTransform transform)
    {
        foreach (var w in tier.GetInfoWaypoints())
        {
            var center = transform.WorldToScreen(w.GetInfoCenterX(), w.GetInfoCenterY());
            var approxRadius = Math.Max(0.1, Math.Min(w.GetInfoLength(), w.GetInfoWidth()) / 2.0);
            var r = MathF.Max(1, transform.WorldToScreenLength(approxRadius * 0.5));
            commands.Add(new FillCircle(center, r, new RenderColor(140, 140, 140)));
        }
    }
}
