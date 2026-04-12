using RAWSimO.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RAWSimO.Core.Elements;

/// <summary>
/// The class serving as the root element for all information about the physical structure of the instance.
/// </summary>
public class Compound : InstanceElement, IUpdateable
{
    private const int ParallelTierMinThreshold = 2;

    #region Constructors

    /// <summary>
    /// Creates a new compound.
    /// </summary>
    /// <param name="instance">The instance this compound belongs to.</param>
    internal Compound(Instance instance) : base(instance) { }

    #endregion

    #region Core

    /// <summary>
    /// All tiers defining this compound.
    /// </summary>
    public readonly List<Tier> Tiers = [];
    /// <summary>
    /// Returns the shortest time without a collision respecting the current situation and speeds.
    /// </summary>
    /// <returns>The shortest time without a collision.</returns>
    public double GetShortestTimeWithoutCollision()
    {
        if (Tiers.Count == 0)
            return double.PositiveInfinity;

        if (Environment.ProcessorCount <= 1 || Tiers.Count < ParallelTierMinThreshold)
            return Tiers.Min(t => t.GetShortestTimeWithoutCollision());

        var globalMin = double.PositiveInfinity;
        var sync = new object();

        Parallel.For<double>(
            0,
            Tiers.Count,
            () => double.PositiveInfinity,
            (index, _, localMin) =>
            {
                var tierMin = Tiers[index].GetShortestTimeWithoutCollision();
                return tierMin < localMin ? tierMin : localMin;
            },
            localMin =>
            {
                lock (sync)
                {
                    if (localMin < globalMin)
                        globalMin = localMin;
                }
            });

        return globalMin;
    }

    #endregion

    #region Meta-Info

    /// <summary>
    /// Saves the current tier of every bot for fast access.
    /// </summary>
    public readonly Dictionary<Bot, Tier> BotCurrentTier = new();

    /// <summary>
    /// Saves the current tier of every pod for fast access.
    /// </summary>
    public readonly Dictionary<Pod, Tier> PodCurrentTier = new();

    #endregion

    #region Inherited methods

    /// <summary>
    /// Returns a simple string identifying this object in its instance.
    /// </summary>
    /// <returns>A simple name identifying the instance element.</returns>
    public override string GetIdentfierString() { return "Compound" + ID; }
    /// <summary>
    /// Returns a simple string giving information about the object.
    /// </summary>
    /// <returns>A simple string.</returns>
    public override string ToString() { return "Compound" + ID; }

    #endregion

    #region IUpdateable Members

    /// <summary>
    /// The next event when this element has to be updated.
    /// </summary>
    /// <param name="currentTime">The current time of the simulation.</param>
    /// <returns>The next time this element has to be updated.</returns>
    public double GetNextEventTime(double currentTime) { return double.PositiveInfinity; }
    /// <summary>
    /// Updates the element to the specified time.
    /// </summary>
    /// <param name="lastTime">The time before the update.</param>
    /// <param name="currentTime">The time to update to.</param>
    public void Update(double lastTime, double currentTime)
    {
        if (Environment.ProcessorCount > 1 && Tiers.Count >= ParallelTierMinThreshold)
        {
            Parallel.For(0, Tiers.Count, i => Tiers[i].Update(lastTime, currentTime));
            return;
        }

        foreach (var tier in Tiers)
            tier.Update(lastTime, currentTime);
    }

    #endregion
}