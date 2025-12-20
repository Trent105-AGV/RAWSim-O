using System.Text;
using System.Text.Json;
using MagicOnion;
using MagicOnion.Server;
using Microsoft.AspNetCore.SignalR;
using RAWSimO.Core;
using RAWSimO.Core.IO;
using RAWSimO.Core.Randomization;
using RAWSimO.Rendering2D;
using RAWSimO.WebServer.Hubs;
using RAWSimO.WebServer.Shared.Hubs;
using RAWSimO.WebServer.Shared.SimulationHost.Types;
using RAWSimO.WebServer.Shared.SimulationHost.Services;
using RAWSimO.WebServer.SimulationHost.Models;

namespace RAWSimO.WebServer.SimulationHost.Services;

public sealed class SimulationHostServices(IHubContext<MessageHub, IMessageClient> hub)
    : ServiceBase<ISimulationHostService>, ISimulationHostService
{
    private readonly Lock _gate = new();
    private readonly Simulation2DCommandBuilder _renderer = new();

    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private Instance? _instance;
    private Exception? _lastError;

    private volatile bool _isPaused;

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

    public UnaryResult<bool> Health()
    {
        return UnaryResult.FromResult(true);
    }

    public UnaryResult<StatusResponse> GetStatus()
    {
        var resp = new StatusResponse(IsRunning, SimTime, LastError);
        return UnaryResult.FromResult(resp);
    }

    public UnaryResult<StartResponse> StartSimulation(StartRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Instance) || string.IsNullOrWhiteSpace(request.Setting) ||
            string.IsNullOrWhiteSpace(request.ControlConfig) ||
            string.IsNullOrWhiteSpace(request.StatisticsDir))
        {
            var resp = new StartResponse(ESimulationStartResult.InvalidConfiguration,
                "Instance/Setting/ControlConfig/StatisticsDir are required");
            return UnaryResult.FromResult(resp);
        }

        StartSimulationNotification? startNotification;

        lock (_gate)
        {
            if (_runTask is { IsCompleted: false })
            {
                var resp = new StartResponse(ESimulationStartResult.AlreadyRunning, "Simulation already running");
                return UnaryResult.FromResult(resp);
            }

            _lastError = null;
            _latestFrame = null;
            _isPaused = false;

            _cts = new CancellationTokenSource();

            try
            {
                Action<string> logAction = Console.WriteLine;

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

                startNotification = new StartSimulationNotification(
                    Instance: request.Instance,
                    Setting: request.Setting,
                    ControlConfig: request.ControlConfig,
                    Seed: seed,
                    Tag: request.Tag,
                    StartedAtUtc: DateTimeOffset.UtcNow
                );

                var token = _cts.Token;
                _runTask = Task.Run(() => RunLoop(instance, token), token);
                var successResp = new StartResponse(ESimulationStartResult.Success);
                _ = SafeBroadcast(() => hub.Clients.All.StartSimulation(startNotification));
                return UnaryResult.FromResult(successResp);
            }
            catch (Exception ex)
            {
                _lastError = ex;
                _instance = null;
                _cts?.Dispose();
                _cts = null;
                _runTask = null;
                var errResp = new StartResponse(ESimulationStartResult.UnknownError, ex.Message);
                return UnaryResult.FromResult(errResp);
            }
        }
    }

    public UnaryResult<bool> EndSimulation()
    {
        CancellationTokenSource? cts;
        double simTime;
        string? error;

        lock (_gate)
        {
            cts = _cts;

            simTime = _instance?.Controller?.CurrentTime ?? 0;
            error = _lastError?.ToString();
            _isPaused = false;
        }

        try
        {
            cts?.Cancel();
        }
        catch
        {
            // ignore
        }

        var endNotification = new EndSimulationNotification(simTime, error, DateTimeOffset.UtcNow);
        _ = SafeBroadcast(() => hub.Clients.All.EndSimulation(endNotification));

        return UnaryResult.FromResult(true);
    }

    public UnaryResult<bool> PauseSimulation()
    {
        double simTime;
        lock (_gate)
        {
            if (_runTask is not { IsCompleted: false } || _instance is null)
                return UnaryResult.FromResult(false);

            _isPaused = true;
            simTime = _instance.Controller.CurrentTime;
        }

        var notification = new PauseSimulationNotification(simTime, DateTimeOffset.UtcNow);
        _ = SafeBroadcast(() => hub.Clients.All.PauseSimulation(notification));
        return UnaryResult.FromResult(true);
    }

    public UnaryResult<bool> ResumeSimulation()
    {
        double simTime;
        lock (_gate)
        {
            if (_runTask is not { IsCompleted: false } || _instance is null)
                return UnaryResult.FromResult(false);

            _isPaused = false;
            simTime = _instance.Controller.CurrentTime;
        }

        var notification = new ResumeSimulationNotification(simTime, DateTimeOffset.UtcNow);
        _ = SafeBroadcast(() => hub.Clients.All.ResumeSimulation(notification));
        return UnaryResult.FromResult(true);
    }

    public UnaryResult<RenderFrameResponse> GetLatestFrame(RenderFrameRequest request)
    {
        var frameDto = GetLatestFrameDto(request.WidthPx, request.HeightPx, request.TierIndex,
            new RenderOptions
            {
                DrawBots = request.DrawBots,
                DrawPods = request.DrawPods,
                DrawStations = request.DrawStations,
                DrawWaypoints = request.DrawWaypoints
            });

        var resp = new RenderFrameResponse(
            frameDto.ViewportWidthPx,
            frameDto.ViewportHeightPx,
            frameDto.TierIndex,
            frameDto.SimTime,
            SerializeFrameData(frameDto)
        );
        return UnaryResult.FromResult(resp);
    }

    private RenderFrameDto GetLatestFrameDto(int widthPx, int heightPx, int tierIndex, RenderOptions? options)
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

    private static byte[]? SerializeFrameData(RenderFrameDto frameDto)
    {
        if (frameDto.Commands.Count == 0)
            return null;

        try
        {
            var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            var json = JsonSerializer.Serialize(frameDto, jsonOptions);
            return Encoding.UTF8.GetBytes(json);
        }
        catch
        {
            return null;
        }
    }

    private void RunLoop(Instance instance, CancellationToken ct)
    {
        try
        {
            const double stepDt = 0.05;
            const int frameEverySteps = 1;
            var steps = 0;

            while (!ct.IsCancellationRequested)
            {
                if (_isPaused)
                {
                    Thread.Sleep(25);
                    continue;
                }

                instance.Controller.Update(stepDt);
                steps++;

                if (steps % frameEverySteps == 0)
                {
                    var frame = BuildFrame(instance, 800, 600, tierIndex: 0, options: null);
                    _latestFrame = frame;
                }

                Thread.Sleep(10);
            }
        }
        catch (Exception ex)
        {
            lock (_gate)
                _lastError = ex;
        }
    }

    private static Task SafeBroadcast(Func<Task> action)
    {
        try
        {
            var task = action();
            _ = task.ContinueWith(static _ => { }, TaskContinuationOptions.OnlyOnFaulted);
            return task;
        }
        catch
        {
            return Task.CompletedTask;
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

            var payload = instance is null
                ? RenderFrameDto.Empty(widthPx, heightPx, tierIndex)
                : BuildFrame(instance, widthPx, heightPx, tierIndex, options);

            var json = JsonSerializer.Serialize(payload, jsonOptions);
            var bytes = Encoding.UTF8.GetBytes($"data: {json}\n\n");
            await response.Body.WriteAsync(bytes, ct);
            await response.Body.FlushAsync(ct);

            await Task.Delay(100, ct);
        }
    }
}
