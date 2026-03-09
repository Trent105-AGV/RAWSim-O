using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;
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

public interface ISimulationStreamService
{
    Task StreamFramesSse(HttpResponse response, int widthPx, int heightPx, int tierIndex,
        RenderOptions? options, CancellationToken ct);

    Task StreamSimulationDataSse(HttpResponse response, CancellationToken ct);
}

public sealed class SimulationHostServices(IHubContext<MessageHub, IMessageClient> hub)
    : ServiceBase<ISimulationHostService>, ISimulationHostService, ISimulationStreamService
{
    private const string StatisticsRootDirectory = "/app/out";

    private static readonly System.Text.RegularExpressions.Regex OutputDirNamePattern =
        new("^[A-Za-z0-9._-]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private readonly Lock _gate = new();
    private readonly Simulation2DCommandBuilder _renderer = new();

    private string? _runInputDirectory;

    private string? _statisticsOutputDirName;

    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private Instance? _instance;
    private Exception? _lastError;

    private StreamWriter? _runLogWriter;
    private int _finalizeOnce;

    private sealed record CurrentRunInputs(
        string Instance,
        string Setting,
        string ControlConfig,
        int Seed,
        string? Tag);

    private CurrentRunInputs? _currentRunInputs;

    private volatile bool _isPaused;

    private volatile RenderFrameDto? _latestFrame;

    private int _preferredViewportWidthPx = 800;
    private int _preferredViewportHeightPx = 600;
    private RenderOptions _currentRenderOptions = new();
    private int _currentRenderTierIndex;

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
        lock (_gate)
        {
            var running = IsRunning;
            var simTime = Math.Round(SimTime, 2);
            var error = LastError;

            var inputs = running ? _currentRunInputs : null;
            var resp = new StatusResponse(
                Running: running,
                SimTime: simTime,
                Error: error,
                Instance: inputs?.Instance,
                Setting: inputs?.Setting,
                ControlConfig: inputs?.ControlConfig,
                Seed: inputs?.Seed,
                Tag: inputs?.Tag);

            return UnaryResult.FromResult(resp);
        }
    }

    public UnaryResult<StartResponse> StartSimulation(StartRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Instance) || string.IsNullOrWhiteSpace(request.Setting) ||
            string.IsNullOrWhiteSpace(request.ControlConfig))
        {
            var resp = new StartResponse(ESimulationStartResult.InvalidConfiguration,
                "Instance/Setting/ControlConfig are required");
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
            _preferredViewportWidthPx = SanitizeViewportDimension(request.WidthPx, 800);
            _preferredViewportHeightPx = SanitizeViewportDimension(request.HeightPx, 600);
            _currentRenderOptions = new RenderOptions
            {
                DrawBots = true,
                DrawPods = true,
                DrawStations = true,
                DrawWaypoints = false
            };
            _currentRenderTierIndex = 0;
            _statisticsOutputDirName = null;
            _currentRunInputs = null;

            if (_runLogWriter is not null)
            {
                try
                {
                    _runLogWriter.Dispose();
                }
                catch
                {
                    /* ignore */
                }

                _runLogWriter = null;
            }

            _finalizeOnce = 0;

            // Clean up previous run inputs if any (only used for inline-content mode).
            TryDeleteDirectory(_runInputDirectory);
            _runInputDirectory = null;

            _cts = new CancellationTokenSource();

            try
            {
                Action<string> logAction = Console.WriteLine;

                var (instancePath, settingPath, controlConfigPath, additionalResourceDirectory, runInputDirectory) =
                    ResolveInputs(request);

                var instance = InstanceIO.ReadInstance(
                    instancePath,
                    settingPath,
                    controlConfigPath,
                    logAction: logAction,
                    additionalResourceDirectory: additionalResourceDirectory);
                instance.SettingConfig.LogAction = logAction;

                var seed = request.Seed ?? instance.SettingConfig.Seed;
                instance.SettingConfig.Seed = seed;
                instance.Randomizer = new RandomizerSimple(seed);

                if (!string.IsNullOrWhiteSpace(request.Tag))
                    instance.Tag = request.Tag;

                // Snapshot the inputs so the UI can restore form state when re-entering the page.
                _currentRunInputs = new CurrentRunInputs(
                    Instance: request.Instance,
                    Setting: request.Setting,
                    ControlConfig: request.ControlConfig,
                    Seed: seed,
                    Tag: request.Tag);

                var statisticsFolder = instance.Name + "-" + instance.SettingConfig.Name + "-" +
                                       instance.ControllerConfig.Name + "-" + seed;

                Directory.CreateDirectory(StatisticsRootDirectory);
                var statisticsDirName = MakeUniqueSubdirectoryName(StatisticsRootDirectory, statisticsFolder);
                _statisticsOutputDirName = statisticsDirName;
                instance.SettingConfig.StatisticsDirectory = Path.Combine(StatisticsRootDirectory, statisticsDirName);

                // Ensure statistics output directory exists and is ready.
                instance.StatReset();

                Directory.CreateDirectory(instance.SettingConfig.StatisticsDirectory);
                var logPath = Path.Combine(instance.SettingConfig.StatisticsDirectory, IOConstants.LOG_FILE);
                var logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };
                _runLogWriter = logWriter;

                instance.SettingConfig.StartTime = DateTime.UtcNow;
                instance.SettingConfig.StopTime = default;

                // Wrap LogAction to write to both console and log file
                instance.SettingConfig.LogAction = msg =>
                {
                    Console.WriteLine(msg);
                    logWriter.WriteLine(msg);
                };

                _instance = instance;

                _runInputDirectory = runInputDirectory;

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
                _statisticsOutputDirName = null;
                _currentRunInputs = null;

                if (_runLogWriter is not null)
                {
                    try
                    {
                        _runLogWriter.Dispose();
                    }
                    catch
                    {
                        /* ignore */
                    }

                    _runLogWriter = null;
                }

                _finalizeOnce = 0;
                _cts?.Dispose();
                _cts = null;
                _runTask = null;
                TryDeleteDirectory(_runInputDirectory);
                _runInputDirectory = null;
                var errResp = new StartResponse(ESimulationStartResult.UnknownError, ex.Message);
                return UnaryResult.FromResult(errResp);
            }
        }
    }

    private void FinalizeRun(Instance instance)
    {
        // Idempotent: can be triggered by StopSimulation and/or normal completion.
        if (Interlocked.CompareExchange(ref _finalizeOnce, 1, 0) != 0)
            return;

        try
        {
            instance.SettingConfig.StopTime = DateTime.UtcNow;
            instance.WriteStatistics();
        }
        catch (Exception ex)
        {
            lock (_gate)
                _lastError ??= ex;
        }
        finally
        {
            try
            {
                _runLogWriter?.Dispose();
            }
            catch
            {
                /* ignore */
            }

            _runLogWriter = null;

            lock (_gate)
            {
                _isPaused = false;
                _instance = null;
                _currentRunInputs = null;

                _cts?.Dispose();
                _cts = null;
                _runTask = null;
            }
        }
    }

    public UnaryResult<EndSimulationResponse> EndSimulation()
    {
        CancellationTokenSource? cts;
        Task? runTask;
        string? runInputDirectory;
        string? statisticsOutputDirName;
        double simTime;
        string? error;
        Instance? instance;

        lock (_gate)
        {
            cts = _cts;

            runTask = _runTask;
            runInputDirectory = _runInputDirectory;
            statisticsOutputDirName = _statisticsOutputDirName;

            instance = _instance;

            simTime = _instance?.Controller?.CurrentTime ?? 0;
            error = _lastError?.ToString();
            _isPaused = false;

            // Let the cleanup happen after the run loop stops.
            _runInputDirectory = null;
        }

        try
        {
            cts?.Cancel();
        }
        catch
        {
            // ignore
        }

        // Wait a bit for the run loop to exit so we can finalize statistics deterministically.
        try
        {
            runTask?.Wait(TimeSpan.FromSeconds(10));
        }
        catch
        {
            // ignore
        }

        if (instance is not null)
        {
            FinalizeRun(instance);
        }

        var endNotification = new EndSimulationNotification(simTime, error, DateTimeOffset.UtcNow);
        _ = SafeBroadcast(() => hub.Clients.All.EndSimulation(endNotification));

        var response = new EndSimulationResponse(statisticsOutputDirName, simTime, error);

        if (string.IsNullOrWhiteSpace(runInputDirectory)) return UnaryResult.FromResult(response);
        if (runTask is not null)
        {
            // ReSharper disable once MethodSupportsCancellation
            _ = runTask.ContinueWith(_ => TryDeleteDirectory(runInputDirectory));
        }
        else
        {
            TryDeleteDirectory(runInputDirectory);
        }

        return UnaryResult.FromResult(response);
    }

    public UnaryResult<DownloadStatisticsResponse> DownloadStatistics(DownloadStatisticsRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        var outputDirName = request.OutputDirName;
        if (string.IsNullOrWhiteSpace(outputDirName))
            throw new ArgumentException("OutputDirName is required", nameof(request));
        if (!OutputDirNamePattern.IsMatch(outputDirName))
            throw new ArgumentException("Invalid OutputDirName", nameof(request));

        var baseFullPath = Path.GetFullPath(StatisticsRootDirectory);
        if (!baseFullPath.EndsWith(Path.DirectorySeparatorChar))
            baseFullPath += Path.DirectorySeparatorChar;

        var targetDir = Path.GetFullPath(Path.Combine(StatisticsRootDirectory, outputDirName));
        if (!targetDir.StartsWith(baseFullPath, StringComparison.Ordinal))
            throw new ArgumentException("Invalid OutputDirName", nameof(request));
        if (!Directory.Exists(targetDir))
            throw new DirectoryNotFoundException("Statistics directory not found");

        // Create zip in memory (safe for typical stat sizes; avoids temp-file lifecycle issues).
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var filePath in Directory.EnumerateFiles(targetDir, "*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(targetDir, filePath);
                var entry = archive.CreateEntry(rel, CompressionLevel.Fastest);
                using var entryStream = entry.Open();
                using var fileStream = File.OpenRead(filePath);
                fileStream.CopyTo(entryStream);
            }
        }

        var bytes = ms.ToArray();
        var resp = new DownloadStatisticsResponse(
            OutputDirName: outputDirName,
            FileName: outputDirName + ".zip",
            ZipBytes: bytes);
        return UnaryResult.FromResult(resp);
    }

    private static string MakeUniqueSubdirectoryName(string rootDir, string preferredName)
    {
        // Avoid collisions in the fixed /app/out root.
        var safePreferred = string.IsNullOrWhiteSpace(preferredName) ? "run" : preferredName;
        var candidate = safePreferred;
        var full = Path.Combine(rootDir, candidate);
        if (!Directory.Exists(full))
            return candidate;

        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        candidate = $"{safePreferred}-{stamp}-{Guid.NewGuid():N}";
        return candidate;
    }

    private sealed record ResolvedInputs(
        string InstancePath,
        string SettingPath,
        string ControlConfigPath,
        string AdditionalResourceDirectory,
        string RunInputDirectory
    );

    private static ResolvedInputs ResolveInputs(StartRequest request)
    {
        // Always treat the incoming values as inline file contents.
        var runDir = Path.Combine(Path.GetTempPath(), "RAWSimO.WebServer", "inputs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(runDir);

        var instanceFile = GuessInstanceFileName(request.Instance);
        const string settingFile = "setting.xsett";
        const string controlFile = "control.xconf";

        var instancePath = Path.Combine(runDir, instanceFile);
        var settingPath = Path.Combine(runDir, settingFile);
        var controlPath = Path.Combine(runDir, controlFile);

        File.WriteAllText(instancePath, request.Instance, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(settingPath, request.Setting, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        File.WriteAllText(controlPath, request.ControlConfig, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        var additionalResourceDirectory = runDir;
        if (request.ResourceZip is { Length: > 0 })
        {
            additionalResourceDirectory = ExtractResourceZipToDirectory(request.ResourceZip, runDir);
        }

        return new ResolvedInputs(
            InstancePath: instancePath,
            SettingPath: settingPath,
            ControlConfigPath: controlPath,
            AdditionalResourceDirectory: additionalResourceDirectory,
            RunInputDirectory: runDir
        );
    }

    private static string ExtractResourceZipToDirectory(byte[] zipBytes, string runDir)
    {
        var extractRoot = Path.Combine(runDir, "resources");
        Directory.CreateDirectory(extractRoot);

        var extractRootFullPath = Path.GetFullPath(extractRoot);
        if (!extractRootFullPath.EndsWith(Path.DirectorySeparatorChar))
            extractRootFullPath += Path.DirectorySeparatorChar;

        using var ms = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: false);

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.FullName))
                continue;

            // Prevent zip-slip by validating the full path is within the extract root.
            var destinationPath = Path.GetFullPath(Path.Combine(extractRoot, entry.FullName));
            if (!destinationPath.StartsWith(extractRootFullPath, StringComparison.Ordinal))
                throw new InvalidOperationException($"Invalid zip entry path: {entry.FullName}");

            // Directory entry
            if (entry.FullName.EndsWith("/", StringComparison.Ordinal) ||
                entry.FullName.EndsWith("\\", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            var parent = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);

            using var entryStream = entry.Open();
            using var fileStream = File.Create(destinationPath);
            entryStream.CopyTo(fileStream);
        }

        // If the zip contains a single top-level directory, use it as the resource root.
        var topFiles = Directory.GetFiles(extractRoot);
        var topDirs = Directory.GetDirectories(extractRoot);
        if (topFiles.Length == 0 && topDirs.Length == 1)
            return topDirs[0];

        return extractRoot;
    }

    private static string GuessInstanceFileName(string content)
    {
        // Prefer the canonical extensions used by RAWSimO docs.
        // Content is expected to be XML (custom extensions like .xinst/.xlayo), but we fall back safely.
        try
        {
            using var sr = new StringReader(content);
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                IgnoreComments = true,
                IgnoreWhitespace = true
            };
            using var xr = XmlReader.Create(sr, settings);

            while (xr.Read())
            {
                if (xr.NodeType != XmlNodeType.Element)
                    continue;

                return xr.Name == "LayoutConfiguration"
                    ? "layout.xlayo"
                    : "instance.xinst";
            }
        }
        catch
        {
            // ignore
        }

        return "instance.xinst";
    }

    private static void TryDeleteDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return;

        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch
        {
            // ignore
        }
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

    public UnaryResult<AppendTasksResponse> AppendTasks(AppendTasksRequest request)
    {
        if (request.Tasks.Count == 0)
            return UnaryResult.FromResult(new AppendTasksResponse(false, 0, Error: "Tasks are required"));

        Instance? instance;
        lock (_gate)
        {
            if (_runTask is not { IsCompleted: false } || _instance is null)
                return UnaryResult.FromResult(new AppendTasksResponse(false, 0,
                    Error: "Simulation must be running before appending tasks"));

            instance = _instance;
        }

        try
        {
            var now = instance.Controller.CurrentTime;
            var dtoOrders = new List<DTOOrder>(request.Tasks.Count);

            foreach (var task in request.Tasks)
            {
                if (task.Positions.Count == 0)
                    continue;

                var dtoOrder = new DTOOrder
                {
                    TimeStamp = task.TimeStamp.HasValue ? Math.Max(task.TimeStamp.Value, now) : now,
                    Positions = []
                };

                foreach (var position in task.Positions)
                {
                    if (position is not { Count: > 0 })
                        continue;

                    dtoOrder.Positions.Add(new DTOOrderPosition
                    {
                        ItemDescriptionID = position.ItemDescriptionId,
                        Count = position.Count
                    });
                }

                if (dtoOrder.Positions.Count > 0)
                    dtoOrders.Add(dtoOrder);
            }

            if (dtoOrders.Count == 0)
                return UnaryResult.FromResult(new AppendTasksResponse(false, 0,
                    Error: "No valid task positions provided"));

            var itemManager = instance.ItemManager;
            var appended = itemManager?.AppendDtoOrders(dtoOrders, now) ?? 0;
            var pendingCount = itemManager?.GetInfoPendingOrderCount() ?? 0;
            var openCount = itemManager?.GetInfoOpenOrders()?.Count() ?? 0;
            var completedCount = itemManager?.GetInfoCompletedOrders()?.Count() ?? 0;

            return UnaryResult.FromResult(new AppendTasksResponse(
                appended > 0,
                appended,
                pendingCount,
                openCount,
                completedCount,
                appended > 0 ? null : "Failed to append tasks"));
        }
        catch (Exception ex)
        {
            return UnaryResult.FromResult(new AppendTasksResponse(false, 0, Error: ex.ToString()));
        }
    }

    public UnaryResult<UpdateRenderOptionsResponse> UpdateRenderOptions(UpdateRenderOptionsRequest request)
    {
        lock (_gate)
        {
            if (_runTask is not { IsCompleted: false } || _instance is null)
                return UnaryResult.FromResult(new UpdateRenderOptionsResponse(
                    false,
                    _currentRenderOptions.DrawBots,
                    _currentRenderOptions.DrawPods,
                    _currentRenderOptions.DrawStations,
                    _currentRenderOptions.DrawWaypoints,
                    _currentRenderTierIndex,
                    "Simulation must be running before updating render options"));

            _currentRenderOptions = new RenderOptions
            {
                DrawBots = request.DrawBots,
                DrawPods = request.DrawPods,
                DrawStations = request.DrawStations,
                DrawWaypoints = request.DrawWaypoints,
                PaddingPx = _currentRenderOptions.PaddingPx
            };

            var tierCount = _instance.Compound?.Tiers?.Count ?? 0;
            if (request.TierIndex.HasValue)
            {
                _currentRenderTierIndex = tierCount > 0
                    ? Math.Clamp(request.TierIndex.Value, 0, tierCount - 1)
                    : 0;
            }

            // Invalidate cached frame to apply updated render style immediately.
            _latestFrame = null;

            return UnaryResult.FromResult(new UpdateRenderOptionsResponse(
                true,
                _currentRenderOptions.DrawBots,
                _currentRenderOptions.DrawPods,
                _currentRenderOptions.DrawStations,
                _currentRenderOptions.DrawWaypoints,
                _currentRenderTierIndex));
        }
    }

    public UnaryResult<RenderFrameResponse> GetLatestFrame(RenderFrameRequest request)
    {
        int tierIndex;
        lock (_gate)
            tierIndex = _currentRenderTierIndex;

        var frameDto = GetLatestFrameDto(request.WidthPx, request.HeightPx, tierIndex, null);

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
        var requestedWidth = SanitizeViewportDimension(widthPx, _preferredViewportWidthPx);
        var requestedHeight = SanitizeViewportDimension(heightPx, _preferredViewportHeightPx);
        var cached = _latestFrame;
        if (cached is not null && cached.ViewportWidthPx == requestedWidth &&
            cached.ViewportHeightPx == requestedHeight &&
            cached.TierIndex == tierIndex)
            return cached;

        Instance? instance;
        lock (_gate)
            instance = _instance;

        if (instance is null)
            return RenderFrameDto.Empty(requestedWidth, requestedHeight, tierIndex);

        return BuildFrame(instance, requestedWidth, requestedHeight, tierIndex, options);
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
            var endTime = instance.SettingConfig.SimulationWarmupTime + instance.SettingConfig.SimulationDuration;

            while (!ct.IsCancellationRequested && instance.Controller.CurrentTime < endTime)
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
                    RenderOptions runOptions;
                    int runTierIndex;
                    lock (_gate)
                    {
                        runOptions = CloneRenderOptions(_currentRenderOptions);
                        runTierIndex = _currentRenderTierIndex;
                    }

                    var frame = BuildFrame(instance, _preferredViewportWidthPx, _preferredViewportHeightPx,
                        tierIndex: runTierIndex, options: runOptions);
                    _latestFrame = frame;
                }

                Thread.Sleep(10);
            }
        }
        catch (Exception ex)
        {
            lock (_gate)
                _lastError ??= ex;
        }
        finally
        {
            FinalizeRun(instance);
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
        var requestedWidth = SanitizeViewportDimension(widthPx, _preferredViewportWidthPx);
        var requestedHeight = SanitizeViewportDimension(heightPx, _preferredViewportHeightPx);
        var (actualWidth, actualHeight) = ResolveViewportSize(instance, tierIndex, requestedWidth, requestedHeight);

        var viewport = new RectF(0, 0, actualWidth, actualHeight);
        var frame = _renderer.Build(instance, tierIndex, viewport, options);
        return RenderFrameDto.From(frame, actualWidth, actualHeight, tierIndex, instance.Controller.CurrentTime);
    }

    public async Task StreamFramesSse(HttpResponse response, int widthPx, int heightPx, int tierIndex,
        RenderOptions? options, CancellationToken ct)
    {
        var requestedWidth = SanitizeViewportDimension(widthPx, _preferredViewportWidthPx);
        var requestedHeight = SanitizeViewportDimension(heightPx, _preferredViewportHeightPx);

        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";
        response.ContentType = "text/event-stream";

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        while (!ct.IsCancellationRequested)
        {
            Instance? instance;
            RenderOptions streamOptions;
            int streamTierIndex;
            lock (_gate)
            {
                instance = _instance;
                streamOptions = CloneRenderOptions(_currentRenderOptions);
                streamTierIndex = _currentRenderTierIndex;
            }

            var payload = instance is null
                ? RenderFrameDto.Empty(requestedWidth, requestedHeight, streamTierIndex)
                : BuildFrame(instance, requestedWidth, requestedHeight, streamTierIndex, streamOptions);

            var json = JsonSerializer.Serialize(payload, jsonOptions);
            var bytes = Encoding.UTF8.GetBytes($"data: {json}\n\n");
            await response.Body.WriteAsync(bytes, ct);
            await response.Body.FlushAsync(ct);

            await Task.Delay(100, ct);
        }
    }

    public async Task StreamSimulationDataSse(HttpResponse response, CancellationToken ct)
    {
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";
        response.ContentType = "text/event-stream";

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        while (!ct.IsCancellationRequested)
        {
            Instance? instance;
            RenderOptions dataOptions;
            int dataTierIndex;
            lock (_gate)
            {
                instance = _instance;
                dataOptions = CloneRenderOptions(_currentRenderOptions);
                dataTierIndex = _currentRenderTierIndex;
            }

            var payload = BuildSimulationData(instance, dataTierIndex, dataOptions);
            var json = JsonSerializer.Serialize(payload, jsonOptions);
            var bytes = Encoding.UTF8.GetBytes($"data: {json}\n\n");
            await response.Body.WriteAsync(bytes, ct);
            await response.Body.FlushAsync(ct);

            await Task.Delay(100, ct);
        }
    }

    private static int SanitizeViewportDimension(int value, int fallback)
    {
        if (value <= 0)
            return Math.Max(1, fallback);

        return value;
    }

    private static RenderOptions CloneRenderOptions(RenderOptions options)
    {
        return new RenderOptions
        {
            DrawBots = options.DrawBots,
            DrawPods = options.DrawPods,
            DrawStations = options.DrawStations,
            DrawWaypoints = options.DrawWaypoints,
            PaddingPx = options.PaddingPx
        };
    }

    private static SimulationDataDto BuildSimulationData(Instance? instance, int tierIndex, RenderOptions options)
    {
        if (instance?.Compound?.Tiers == null || instance.Compound.Tiers.Count == 0)
            return SimulationDataDto.Empty(tierIndex, 1, 1);

        var validTierIndex = Math.Clamp(tierIndex, 0, instance.Compound.Tiers.Count - 1);
        var tier = instance.Compound.Tiers[validTierIndex];

        var bots = options.DrawBots
            ? tier.CurrentBots.Select(b => new SimulationCircleDto(b.ID, b.X, b.Y, b.Radius, b.Orientation)).ToArray()
            : [];

        var pods = options.DrawPods
            ? tier.CurrentPods.Select(p => new SimulationCircleDto(p.ID, p.X, p.Y, p.Radius)).ToArray()
            : [];

        var inputStations = options.DrawStations
            ? instance.InputStations.Where(s => ReferenceEquals(s.Tier, tier))
                .Select(s => new SimulationCircleDto(s.ID, s.X, s.Y, s.Radius)).ToArray()
            : [];

        var outputStations = options.DrawStations
            ? instance.OutputStations.Where(s => ReferenceEquals(s.Tier, tier))
                .Select(s => new SimulationCircleDto(s.ID, s.X, s.Y, s.Radius)).ToArray()
            : [];

        var waypoints = options.DrawWaypoints
            ? instance.Waypoints.Where(w => ReferenceEquals(w.Tier, tier))
                .Select(w => new SimulationPointDto(w.ID, w.X, w.Y)).ToArray()
            : [];

        var pendingCount = instance.ItemManager?.GetInfoPendingOrderCount() ?? 0;
        var openCount = instance.ItemManager?.GetInfoOpenOrders()?.Count() ?? 0;
        var completedCount = instance.ItemManager?.GetInfoCompletedOrders()?.Count() ?? 0;

        return new SimulationDataDto(
            TierIndex: validTierIndex,
            SimTime: instance.Controller?.CurrentTime ?? 0,
            WorldWidth: tier.Length,
            WorldHeight: tier.Width,
            PendingOrderCount: pendingCount,
            OpenOrderCount: openCount,
            CompletedOrderCount: completedCount,
            Bots: bots,
            Pods: pods,
            InputStations: inputStations,
            OutputStations: outputStations,
            Waypoints: waypoints);
    }

    private static (int Width, int Height) ResolveViewportSize(Instance instance, int tierIndex, int requestedWidth,
        int requestedHeight)
    {
        if (instance.Compound?.Tiers == null || instance.Compound.Tiers.Count == 0)
            return (requestedWidth, requestedHeight);

        var validTierIndex = Math.Clamp(tierIndex, 0, instance.Compound.Tiers.Count - 1);
        var tier = instance.Compound.Tiers[validTierIndex];
        if (tier == null || tier.Width <= 0 || tier.Length <= 0)
            return (requestedWidth, requestedHeight);

        var aspect = tier.Length / tier.Width;
        if (aspect <= 0 || double.IsNaN(aspect) || double.IsInfinity(aspect))
            return (requestedWidth, requestedHeight);

        var widthByHeight = (int)Math.Round(requestedHeight * aspect);
        var heightByWidth = (int)Math.Round(requestedWidth / aspect);

        if (widthByHeight <= requestedWidth)
            return (Math.Max(1, widthByHeight), requestedHeight);

        return (requestedWidth, Math.Max(1, heightByWidth));
    }
}
