using RAWSimO.Core.Control.Defaults.ReplenishmentBatching;
using System;

namespace RAWSimO.Core.Configurations;

#region Replenishment batching configurations

/// <summary>
/// The configuration for the corresponding method.
/// </summary>
public class RandomReplenishmentBatchingConfiguration : ReplenishmentBatchingConfiguration
{
    /// <summary>
    /// Returns the type of the corresponding method this configuration belongs to.
    /// </summary>
    /// <returns>The type of the method.</returns>
    public override ReplenishmentBatchingMethodType GetMethodType() { return ReplenishmentBatchingMethodType.Random; }
    /// <summary>
    /// Returns a name identifying the method.
    /// </summary>
    /// <returns>The name of the method.</returns>
    public override string GetMethodName() { if (!string.IsNullOrWhiteSpace(Name)) return Name; return "rbR" + (Recycle == true ? "t" : "f"); }
    /// <summary>
    /// Indicates whether stations are recycled, i.e. one station is filled with bundles as long as there is capacity left.
    /// </summary>
    public readonly bool Recycle = true;

}
/// <summary>
/// The configuration for the corresponding method.
/// </summary>
public class SamePodReplenishmentBatchingConfiguration : ReplenishmentBatchingConfiguration
{
    /// <summary>
    /// Returns the type of the corresponding method this configuration belongs to.
    /// </summary>
    /// <returns>The type of the method.</returns>
    public override ReplenishmentBatchingMethodType GetMethodType() { return ReplenishmentBatchingMethodType.SamePod; }
    /// <summary>
    /// Returns a name identifying the method.
    /// </summary>
    /// <returns>The name of the method.</returns>
    public override string GetMethodName()
    {
        if (!string.IsNullOrWhiteSpace(Name)) return Name;
        var name = "rbSP";
        name += FirstStationRule switch
        {
            SamePodFirstStationRule.Emptiest => "e",
            SamePodFirstStationRule.Fullest => "f",
            SamePodFirstStationRule.LeastBusy => "l",
            SamePodFirstStationRule.MostBusy => "m",
            SamePodFirstStationRule.Random => "r",
            SamePodFirstStationRule.DistanceEuclid => "d",
            _ => throw new ArgumentException("Unexpected argument!")
        };
        name += BreakBatches ? "t" : "f";
        return name;
    }
    /// <summary>
    /// Indicates how the first station is selected for a set of incoming bundles. If all bundles fit the station, it is the only station used for the bundles.
    /// </summary>
    public SamePodFirstStationRule FirstStationRule = SamePodFirstStationRule.DistanceEuclid;
    /// <summary>
    /// Tells the mechanism whether batches of bundles for a single pod can be divided across multiple stations at all.
    /// </summary>
    public readonly bool BreakBatches = false;
    /// <summary>
    /// Tells the mechanism to process batches in the order they arrive instead of aiming to allocate them as quickly as possible.
    /// </summary>
    public readonly bool FCFS = true;
    /// <summary>
    /// Tells the mechanism to only accept pods for an input station that are located on the same tier.
    /// </summary>
    public bool OnlySameTier = true;
}

#endregion