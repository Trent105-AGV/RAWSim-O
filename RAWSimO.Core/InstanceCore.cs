using RAWSimO.Core.Bots;
using RAWSimO.Core.Configurations;
using RAWSimO.Core.Control.Shared;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Items;
using RAWSimO.Core.Management;
using RAWSimO.Core.Statistics;
using RAWSimO.Core.Waypoints;
using System.Collections.Generic;

namespace RAWSimO.Core;

/// THIS PARTIAL CLASS CONTAINS THE CORE FIELDS OF AN INSTANCE
/// <summary>
/// The core element of each simulation instance.
/// </summary>
public partial class Instance
{
    #region Constructors

    internal Instance()
    {
        Observer = new SimulationObserver(this);
        StockInfo = new StockInformation(this);
        MetaInfoManager = new MetaInformationManager(this);
        FrequencyTracker = new FrequencyTracker(this);
        ElementMetaInfoTracker = new ElementMetaInfoTracker(this);
        BotCrashHandler = new BotCrashHandler(this);
        SharedControlElements = new SharedControlElementsContainer(this);
    }

    #endregion

    #region Core

    /// <summary>
    /// The name of the instance.
    /// </summary>
    public string Name;
    /// <summary>
    /// The configuration to use while executing the instance.
    /// </summary>
    public SettingConfiguration SettingConfig { get; set; }
    /// <summary>
    /// The configuration for all controlling mechanisms.
    /// </summary>
    public ControlConfiguration ControllerConfig { get; set; }
    /// <summary>
    /// All SKUs available in this instance.
    /// </summary>
    public readonly List<ItemDescription> ItemDescriptions = [];
    /// <summary>
    /// All item bundles known so far.
    /// </summary>
    public readonly List<ItemBundle> ItemBundles = [];
    /// <summary>
    /// A list of given orders that will be passed to the item manager.
    /// </summary>
    public OrderList OrderList;
    /// <summary>
    /// The compound declaring all physical attributes of the instance.
    /// </summary>
    public Compound Compound;
    /// <summary>
    /// All robots of this instance.
    /// </summary>
    public readonly List<Bot> Bots = [];
    /// <summary>
    /// All pods of this instance.
    /// </summary>
    public readonly List<Pod> Pods = [];
    /// <summary>
    /// All elevators of this instance.
    /// </summary>
    public readonly List<Elevator> Elevators = [];
    /// <summary>
    /// All input-stations of this instance.
    /// </summary>
    public readonly List<InputStation> InputStations = [];
    /// <summary>
    /// All output-stations of this instance.
    /// </summary>
    public readonly List<OutputStation> OutputStations = [];
    /// <summary>
    /// All waypoints of this instance.
    /// </summary>
    public readonly List<Waypoint> Waypoints = [];
    /// <summary>
    /// All semaphors of this instance.
    /// </summary>
    public readonly List<QueueSemaphore> Semaphores = [];

    #endregion
}