using RAWSimO.Core.Info;

namespace RAWSimO.Visualization.Rendering;

public abstract class SimulationAnimation
{
    public SimulationAnimation(IInstanceInfo instance, Dispatcher uiDispatcher, SimulationAnimationConfig config, Func<BotColorMode> botColorModeGetter, Func<bool> heatModeEnabled)
    {
        _config = config;
        _dispatcher = uiDispatcher;
        _heatModeEnabled = heatModeEnabled;
        _botColorModeGetter = botColorModeGetter;
        _instance = instance;
        // Init colors for current bots
        List<IBotInfo> bots = instance.GetInfoBots().ToList();
        for (int i = 0; i < bots.Count; i++)
            _rainbowBotColors[bots[i]] = new SolidColorBrush(HeatVisualizer.GenerateBiChromaticHeatColor(Colors.Purple, Colors.Red, (double)i / (bots.Count - 1)));
    }

    protected readonly IInstanceInfo _instance;
    protected Dispatcher _dispatcher;
    protected readonly SimulationAnimationConfig _config;
    protected readonly Func<BotColorMode> _botColorModeGetter;
    protected readonly Func<bool> _heatModeEnabled;

    private Dictionary<IBotInfo, Brush> _rainbowBotColors = new Dictionary<IBotInfo, Brush>();
    public Brush GetBotColor(IBotInfo bot, string state)
    {
        // Check whether we already generated a color for the bot
        if (!_rainbowBotColors.ContainsKey(bot))
        {
            Random randomizer = new Random();
            _rainbowBotColors[bot] = new SolidColorBrush(HeatVisualizer.GenerateBiChromaticHeatColor(Colors.Purple, Colors.Red, randomizer.NextDouble()));
        }
        // Return color
        BotColorMode colorMode = _botColorModeGetter();
        return colorMode switch
        {
            BotColorMode.DefaultBotDefaultState => VisualizationConstants.StateBrushes[state],
            BotColorMode.RainbowBotSingleState => state == "Move"
                ? _rainbowBotColors[bot]
                : VisualizationConstants.StateBrushHidden,
            BotColorMode.RainbowBotDefaultState => state == "Move"
                ? _rainbowBotColors[bot]
                : VisualizationConstants.StateBrushes[state],
            _ => throw new ArgumentException("Unknown bot coloring mode: " + colorMode.ToString())
        };
    }

    public abstract void Init();
    public abstract void Update(bool overrideUpdate);
    public abstract void StopAnimation();
    public abstract void TakeSnapshot(string snapshotDir, string snapshotFilename = null);
}