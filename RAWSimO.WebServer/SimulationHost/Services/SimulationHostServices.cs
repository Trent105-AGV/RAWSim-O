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

    private sealed record CurrentRunInputs(
        string Instance,
        string Setting,
        string ControlConfig,
        int Seed,
        string? Tag);

    private CurrentRunInputs? _currentRunInputs;

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
        lock (_gate)
        {
            var running = _runTask is { IsCompleted: false };
            var simTime = _instance?.Controller?.CurrentTime ?? 0;
            var error = _lastError?.ToString();

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
            _statisticsOutputDirName = null;
            _currentRunInputs = null;

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

                Directory.CreateDirectory(instance.SettingConfig.StatisticsDirectory);
                var logPath = Path.Combine(instance.SettingConfig.StatisticsDirectory, IOConstants.LOG_FILE);
                var logWriter = new StreamWriter(logPath, append: false) { AutoFlush = true };

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
                _ = _runTask.ContinueWith(OnRunTaskCompleted, CancellationToken.None,
                    TaskContinuationOptions.None, TaskScheduler.Default);
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

    private void OnRunTaskCompleted(Task task)
    {
        // The simulation lifetime must not depend on any client connections.
        // When the run loop ends (StopSimulation or crash), clear the in-memory instance and inputs.
        lock (_gate)
        {
            if (task.IsFaulted && task.Exception is not null)
                _lastError ??= task.Exception.GetBaseException();

            _isPaused = false;
            _instance = null;
            _currentRunInputs = null;

            _cts?.Dispose();
            _cts = null;
            _runTask = null;
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

        lock (_gate)
        {
            cts = _cts;

            runTask = _runTask;
            runInputDirectory = _runInputDirectory;
            statisticsOutputDirName = _statisticsOutputDirName;

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
