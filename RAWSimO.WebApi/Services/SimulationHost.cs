using System.Text;
using System.Text.Json;
using RAWSimO.Core;
using RAWSimO.Core.IO;
using RAWSimO.Core.Randomization;
using RAWSimO.Rendering2D;

namespace RAWSimO.WebApi.Services;

public sealed class SimulationHost
{
    private readonly object _gate = new();
    private readonly Simulation2DCommandBuilder _renderer = new();

    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private Instance? _instance;
    private Exception? _lastError;

    private volatile RenderFrameDto? _latestFrame;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
                return _runTask is { IsCompleted: false };
        }
    }

    public double SimTime
    {
        get
        {
            lock (_gate)
                return _instance?.Controller?.CurrentTime ?? 0;
        }
    }

    public string? LastError
    {
        get
        {
            lock (_gate)
                return _lastError?.ToString();
        }
    }

    public RenderFrameDto GetLatestFrame(int widthPx, int heightPx, int tierIndex, RenderOptions? options)
    {
        var cached = _latestFrame;
        if (cached is not null && cached.ViewportWidthPx == widthPx && cached.ViewportHeightPx == heightPx &&
            cached.TierIndex == tierIndex)
            return cached;

        Instance? instance;
        lock (_gate)
            instance = _instance;

        if (instance is null)
            return RenderFrameDto.Empty(widthPx, heightPx, tierIndex);

        return BuildFrame(instance, widthPx, heightPx, tierIndex, options);
    }

    public bool TryStart(StartRequest request, out string? error)
    {
        error = null;

        lock (_gate)
        {
            if (_runTask is { IsCompleted: false })
            {
                error = "Simulation already running";
                return false;
            }

            _lastError = null;
            _latestFrame = null;

            _cts = new CancellationTokenSource();

            try
            {
                Action<string> logAction = msg => Console.WriteLine(msg);

                var instance = InstanceIO.ReadInstance(request.Instance, request.Setting, request.ControlConfig,
                    logAction: logAction);
                instance.SettingConfig.LogAction = logAction;

                var seed = request.Seed ?? instance.SettingConfig.Seed;
                instance.SettingConfig.Seed = seed;
                instance.Randomizer = new RandomizerSimple(seed);

                if (!string.IsNullOrWhiteSpace(request.Tag))
                    instance.Tag = request.Tag;

                var statisticsFolder = instance.Name + "-" + instance.SettingConfig.Name + "-" +
                                       instance.ControllerConfig.Name + "-" + seed;
                instance.SettingConfig.StatisticsDirectory = Path.Combine(request.StatisticsDir, statisticsFolder);

                Directory.CreateDirectory(instance.SettingConfig.StatisticsDirectory);
                var logPath = Path.Combine(instance.SettingConfig.StatisticsDirectory, IOConstants.LOG_FILE);
                _ = new StreamWriter(logPath, append: false) { AutoFlush = true };

                _instance = instance;

                _runTask = Task.Run(() => RunLoop(instance, _cts.Token), _cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                _lastError = ex;
                _instance = null;
                _cts?.Dispose();
                _cts = null;
                _runTask = null;
                error = ex.Message;
                return false;
            }
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;
        lock (_gate)
            cts = _cts;

        try
        {
            cts?.Cancel();
        }
        catch
        {
            // ignore
        }
    }

    private void RunLoop(Instance instance, CancellationToken ct)
    {
        try
        {
            const double stepDt = 0.05;
            const int frameEverySteps = 1;
            int steps = 0;

            while (!ct.IsCancellationRequested)
            {
                instance.Controller.Update(stepDt);
                steps++;

                if (steps % frameEverySteps == 0)
                {
                    // Default viewport for caching: 800x600, tier 0
                    var frame = BuildFrame(instance, 800, 600, tierIndex: 0, options: null);
                    _latestFrame = frame;
                }

                // throttle a bit to avoid burning CPU
                Thread.Sleep(10);
            }
        }
        catch (Exception ex)
        {
            lock (_gate)
                _lastError = ex;
        }
    }

    private RenderFrameDto BuildFrame(Instance instance, int widthPx, int heightPx, int tierIndex,
        RenderOptions? options)
    {
        var viewport = new RectF(0, 0, widthPx, heightPx);
        var frame = _renderer.Build(instance, tierIndex, viewport, options);
        return RenderFrameDto.From(frame, widthPx, heightPx, tierIndex, instance.Controller.CurrentTime);
    }

    public async Task StreamFramesSse(HttpResponse response, int widthPx, int heightPx, int tierIndex,
        RenderOptions? options, CancellationToken ct)
    {
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";
        response.ContentType = "text/event-stream";

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        while (!ct.IsCancellationRequested)
        {
            Instance? instance;
            lock (_gate)
                instance = _instance;

            RenderFrameDto payload = instance is null
                ? RenderFrameDto.Empty(widthPx, heightPx, tierIndex)
                : BuildFrame(instance, widthPx, heightPx, tierIndex, options);

            var json = JsonSerializer.Serialize(payload, jsonOptions);
            var bytes = Encoding.UTF8.GetBytes($"data: {json}\n\n");
            await response.Body.WriteAsync(bytes, ct);
            await response.Body.FlushAsync(ct);

            await Task.Delay(100, ct); // ~10 fps
        }
    }
}

public sealed record StartRequest(
    string Instance,
    string Setting,
    string ControlConfig,
    string StatisticsDir,
    int? Seed,
    string? Tag
);

public sealed record StatusResponse(bool Running, double SimTime, string? Error);

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

public sealed record RenderCommandDto(string Kind, object Payload)
{
    public static RenderCommandDto From(RenderCommand command) => command switch
    {
        FillRect c => new("FillRect", new FillRectPayload(c.Rect, c.Color)),
        StrokeRect c => new("StrokeRect", new StrokeRectPayload(c.Rect, c.Color, c.Thickness)),
        FillCircle c => new("FillCircle", new FillCirclePayload(c.Center, c.Radius, c.Color)),
        StrokeCircle c => new("StrokeCircle", new StrokeCirclePayload(c.Center, c.Radius, c.Color, c.Thickness)),
        Line c => new("Line", new LinePayload(c.From, c.To, c.Color, c.Thickness)),
        _ => new("Unknown", new { })
    };

    public sealed record FillRectPayload(RectF Rect, RenderColor Color);

    public sealed record StrokeRectPayload(RectF Rect, RenderColor Color, float Thickness);

    public sealed record FillCirclePayload(PointF Center, float Radius, RenderColor Color);

    public sealed record StrokeCirclePayload(PointF Center, float Radius, RenderColor Color, float Thickness);

    public sealed record LinePayload(PointF From, PointF To, RenderColor Color, float Thickness);
}
