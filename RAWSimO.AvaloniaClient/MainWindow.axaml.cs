using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using RAWSimO.Core;
using RAWSimO.Core.IO;
using RAWSimO.Core.Randomization;
using RAWSimO.Rendering2D;

namespace RAWSimO.AvaloniaClient;

public partial class MainWindow : Window
{
    private readonly StringBuilder _log = new();
    private StreamWriter _logWriter;

    private CancellationTokenSource _cts;
    private Task _runTask;

    private Instance _instance;

    private readonly RenderOptions _renderOptions = new()
        { DrawStations = true, DrawPods = true, DrawBots = true, DrawWaypoints = false };

    private readonly DispatcherTimer _uiTimer;

    public MainWindow()
    {
        InitializeComponent();

        _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _uiTimer.Tick += (_, __) =>
        {
            if (_instance != null)
                SimTimeText.Text = "Time: " +
                                   _instance.Controller.CurrentTime.ToString("0.###", CultureInfo.InvariantCulture);

            SimView.InvalidateVisual();
        };
        _uiTimer.Start();
    }

    private void LogLine(string line)
    {
        lock (_log)
        {
            _log.AppendLine(line);
            _logWriter?.WriteLine(line);
        }

        Dispatcher.UIThread.Post(() =>
        {
            LogText.Text = _log.ToString();
            LogText.CaretIndex = LogText.Text?.Length ?? 0;
        });
    }

    private async Task<string> PickFileAsync(string title)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null)
            return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
            });

        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    private async Task<string> PickFolderAsync(string title)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null)
            return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
            });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    private async void BrowseInstance_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFileAsync("Select instance file");
        if (!string.IsNullOrWhiteSpace(path))
            InstancePathText.Text = path;
    }

    private async void BrowseSetting_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFileAsync("Select setting config file");
        if (!string.IsNullOrWhiteSpace(path))
            SettingPathText.Text = path;
    }

    private async void BrowseControl_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFileAsync("Select control config file");
        if (!string.IsNullOrWhiteSpace(path))
            ControlPathText.Text = path;
    }

    private async void BrowseStatisticsDir_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync("Select statistics output folder");
        if (!string.IsNullOrWhiteSpace(path))
            StatisticsDirText.Text = path;
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_runTask != null && !_runTask.IsCompleted)
            return;

        var instancePath = InstancePathText.Text ?? string.Empty;
        var settingPath = SettingPathText.Text ?? string.Empty;
        var controlPath = ControlPathText.Text ?? string.Empty;
        var statisticsDir = StatisticsDirText.Text ?? string.Empty;

        if (!int.TryParse(SeedText.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed))
        {
            StatusText.Text = "Invalid seed";
            return;
        }

        if (string.IsNullOrWhiteSpace(instancePath) || string.IsNullOrWhiteSpace(settingPath) ||
            string.IsNullOrWhiteSpace(controlPath) || string.IsNullOrWhiteSpace(statisticsDir))
        {
            StatusText.Text = "Missing configuration";
            return;
        }

        try
        {
            _cts = new CancellationTokenSource();

            LogLine("<<< Welcome to the RAWSimO Avalonia Client >>>");
            LogLine("The time is: " + DateTime.Now.ToString(IOConstants.FORMATTER));

            Action<string> logAction = LogLine;

            _instance = InstanceIO.ReadInstance(instancePath, settingPath, controlPath, logAction: logAction);
            _instance.SettingConfig.LogAction = logAction;
            _instance.SettingConfig.Seed = seed;
            _instance.Randomizer = new RandomizerSimple(seed);

            var statisticsFolder = _instance.Name + "-" + _instance.SettingConfig.Name + "-" +
                                   _instance.ControllerConfig.Name + "-" + _instance.SettingConfig.Seed;
            _instance.SettingConfig.StatisticsDirectory = Path.Combine(statisticsDir, statisticsFolder);

            Directory.CreateDirectory(_instance.SettingConfig.StatisticsDirectory);
            _logWriter?.Dispose();
            _logWriter =
                new StreamWriter(Path.Combine(_instance.SettingConfig.StatisticsDirectory, IOConstants.LOG_FILE), false)
                    { AutoFlush = true };

            SimView.Instance = _instance;
            SimView.Options = _renderOptions;

            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            StatusText.Text = "Running";

            _runTask = Task.Run(() => RunSimulationIncremental(_instance, _cts.Token), _cts.Token)
                .ContinueWith(t =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        StartButton.IsEnabled = true;
                        StopButton.IsEnabled = false;
                        StatusText.Text = t.IsCanceled ? "Canceled" : (t.IsFaulted ? "Error" : "Finished");
                    });

                    if (t.Exception != null)
                        LogLine(t.Exception.ToString());
                }, TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Error";
            LogLine(ex.ToString());

            StartButton.IsEnabled = true;
            StopButton.IsEnabled = false;
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        StatusText.Text = "Stopping";
    }

    private static void StepUpdate(Instance instance, double totalTime, CancellationToken token)
    {
        var step = 0.05; // seconds
        var remaining = totalTime;

        while (remaining > 0)
        {
            token.ThrowIfCancellationRequested();
            var dt = Math.Min(step, remaining);
            instance.Controller.Update(dt);
            remaining -= dt;
        }
    }

    private void RunSimulationIncremental(Instance instance, CancellationToken token)
    {
        try
        {
            LogLine(">>> Warming up ...");
            instance.StartExecutionTiming();
            StepUpdate(instance, instance.SettingConfig.SimulationWarmupTime, token);

            LogLine(">>> Warmup finished - starting simulation ...");
            instance.StatReset();
            StepUpdate(instance, instance.SettingConfig.SimulationDuration, token);

            instance.StopExecutionTiming();
            LogLine(">>> Simulation finished - writing results ...");
            instance.WriteStatistics();
            LogLine(">>> Results written");
        }
        catch (OperationCanceledException)
        {
            try
            {
                instance.StopExecutionTiming();
            }
            catch
            {
            }

            LogLine(">>> Stopped by user");
            try
            {
                instance.WriteStatistics();
                LogLine(">>> Partial results written");
            }
            catch (Exception ex)
            {
                LogLine(">>> Failed writing partial results: " + ex.Message);
            }

            throw;
        }
    }
}
