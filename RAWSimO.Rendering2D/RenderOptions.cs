namespace RAWSimO.Rendering2D;

public sealed class RenderOptions
{
    public bool DrawWaypoints { get; set; } = false;
    public bool DrawPods { get; set; } = true;
    public bool DrawBots { get; set; } = true;
    public bool DrawStations { get; set; } = true;
    public float PaddingPx { get; set; } = 10;
}
