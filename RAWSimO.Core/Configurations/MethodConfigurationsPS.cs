using RAWSimO.Core.Control.Defaults.PodStorage;
using RAWSimO.Core.Control.Shared;
using RAWSimO.Core.IO;
using System;
using System.Linq;

namespace RAWSimO.Core.Configurations;

#region Pod storage configurations

/// <summary>
/// The configuration for the corresponding method.
/// </summary>
public class DummyPodStorageConfiguration : PodStorageConfiguration
{
    /// <summary>
    /// Returns the type of the corresponding method this configuration belongs to.
    /// </summary>
    /// <returns>The type of the method.</returns>
    public override PodStorageMethodType GetMethodType() { return PodStorageMethodType.Dummy; }
    /// <summary>
    /// Returns a name identifying the method.
    /// </summary>
    /// <returns>The name of the method.</returns>
    public override string GetMethodName() { if (!string.IsNullOrWhiteSpace(Name)) return Name; return "psD"; }
}
/// <summary>
/// The configuration for the corresponding method.
/// </summary>
public class RandomPodStorageConfiguration : PodStorageConfiguration
{
    /// <summary>
    /// Returns the type of the corresponding method this configuration belongs to.
    /// </summary>
    /// <returns>The type of the method.</returns>
    public override PodStorageMethodType GetMethodType() { return PodStorageMethodType.Random; }
    /// <summary>
    /// Returns a name identifying the method.
    /// </summary>
    /// <returns>The name of the method.</returns>
    public override string GetMethodName() { if (!string.IsNullOrWhiteSpace(Name)) return Name; return "psR" + (PreferSameTier ? "t" : "f"); }
    /// <summary>
    /// Indicates whether the controller prefers storage locations of the same tier over others. Locations of the same tier are still chosen randomly.
    /// </summary>
    public readonly bool PreferSameTier = true;
}
/// <summary>
/// The configuration for the corresponding method.
/// </summary>
public class FixedPodStorageConfiguration : PodStorageConfiguration
{
    /// <summary>
    /// Returns the type of the corresponding method this configuration belongs to.
    /// </summary>
    /// <returns>The type of the method.</returns>
    public override PodStorageMethodType GetMethodType() { return PodStorageMethodType.Fixed; }
    /// <summary>
    /// Returns a name identifying the method.
    /// </summary>
    /// <returns>The name of the method.</returns>
    public override string GetMethodName() { if (!string.IsNullOrWhiteSpace(Name)) return Name; return "psF"; }
}
/// <summary>
/// The configuration for the corresponding method.
/// </summary>
public class NearestPodStorageConfiguration : PodStorageConfiguration
{
    /// <summary>
    /// Returns the type of the corresponding method this configuration belongs to.
    /// </summary>
    /// <returns>The type of the method.</returns>
    public override PodStorageMethodType GetMethodType() { return PodStorageMethodType.Nearest; }
    /// <summary>
    /// Returns a name identifying the method.
    /// </summary>
    /// <returns>The name of the method.</returns>
    public override string GetMethodName()
    {
        if (!string.IsNullOrWhiteSpace(Name)) return Name;
        var name = "psN";
        name += PodDisposeRule switch
        {
            NearestPodStorageLocationDisposeRule.Euclid => "e",
            NearestPodStorageLocationDisposeRule.Manhattan => "m",
            NearestPodStorageLocationDisposeRule.ShortestPath => "s",
            NearestPodStorageLocationDisposeRule.ShortestTime => "t",
            _ => throw new ArgumentException("Unexpected argument!")
        };
        return name;
    }
    /// <summary>
    /// Indicates which distance metric is used to select a free pod storage location.
    /// </summary>
    public readonly NearestPodStorageLocationDisposeRule PodDisposeRule = NearestPodStorageLocationDisposeRule.ShortestTime;
}
/// <summary>
/// The configuration for the corresponding method.
/// </summary>
public class StationBasedPodStorageConfiguration : PodStorageConfiguration
{
    /// <summary>
    /// Returns the type of the corresponding method this configuration belongs to.
    /// </summary>
    /// <returns>The type of the method.</returns>
    public override PodStorageMethodType GetMethodType() { return PodStorageMethodType.StationBased; }
    /// <summary>
    /// Returns a name identifying the method.
    /// </summary>
    /// <returns>The name of the method.</returns>
    public override string GetMethodName()
    {
        if (!string.IsNullOrWhiteSpace(Name)) return Name;
        var name = "psSB" + (OutputStationMode ? "t" : "f");
        name += PodDisposeRule switch
        {
            StationBasedPodStorageLocationDisposeRule.Euclid => "e",
            StationBasedPodStorageLocationDisposeRule.Manhattan => "m",
            StationBasedPodStorageLocationDisposeRule.ShortestPath => "s",
            StationBasedPodStorageLocationDisposeRule.ShortestTime => "t",
            _ => throw new ArgumentException("Unexpected argument!")
        };
        return name;
    }
    /// <summary>
    /// Indicates whether to store the pods near the output-stations or the input-stations.
    /// </summary>
    public readonly bool OutputStationMode = true;
    /// <summary>
    /// Indicates which distance metric is used to select a free pod storage location.
    /// </summary>
    public readonly StationBasedPodStorageLocationDisposeRule PodDisposeRule = StationBasedPodStorageLocationDisposeRule.ShortestTime;
}
/// <summary>
/// The configuration for the corresponding method.
/// </summary>
public class CachePodStorageConfiguration : PodStorageConfiguration
{
    /// <summary>
    /// Returns the type of the corresponding method this configuration belongs to.
    /// </summary>
    /// <returns>The type of the method.</returns>
    public override PodStorageMethodType GetMethodType() { return PodStorageMethodType.Cache; }
    /// <summary>
    /// Returns a name identifying the method.
    /// </summary>
    /// <returns>The name of the method.</returns>
    public override string GetMethodName()
    {
        if (!string.IsNullOrWhiteSpace(Name)) return Name;
        var name = "psHC";
        name += ZoningConfiguration.DropoffCount.ToString(IOConstants.FORMATTER);
        name += ZoningConfiguration.CacheFraction.ToString(IOConstants.EXPORT_FORMAT_SHORTER, IOConstants.FORMATTER);
        name += WeightSpeed.ToString(IOConstants.EXPORT_FORMAT_SHORTER, IOConstants.FORMATTER);
        name += WeightUtility.ToString(IOConstants.EXPORT_FORMAT_SHORTER, IOConstants.FORMATTER);
        return name;
    }
    /// <summary>
    /// The config to use for creating the different zones.
    /// </summary>
    public readonly CacheConfiguration ZoningConfiguration = new();
    /// <summary>
    /// The weight for the utility score of the pod.
    /// </summary>
    public double WeightUtility = 1;
    /// <summary>
    /// The weight for the speed score of the pod.
    /// </summary>
    public double WeightSpeed = 0;
    /// <summary>
    /// The weight of the current cache fill level when deciding whether to store a pod in the cache (compared to the already stored pods).
    /// </summary>
    public readonly double WeightCacheFill = 1;
    /// <summary>
    /// The weight of the utility when deciding whether to store a pod in the cache (compared to the already stored pods).
    /// </summary>
    public readonly double WeightCacheUtility = 1;
    /// <summary>
    /// If the combined value of cache-fill score and utility of the pod (value is of range [0,1]) is higher than this threshold the pod will be brought to the cache.
    /// </summary>
    public readonly double PodCacheableThreshold = 0.5;
    /// <summary>
    /// The rule to use for selecting the storage location for the pod. This is superimposed by the decision about whether a pod is stored in the cache or not.
    /// </summary>
    public readonly CacheStorageLocationSelectionRule PodDisposeRule = CacheStorageLocationSelectionRule.ShortestTime;
}
/// <summary>
/// The configuration for the corresponding method.
/// </summary>
public class UtilityPodStorageConfiguration : PodStorageConfiguration
{
    /// <summary>
    /// Returns the type of the corresponding method this configuration belongs to.
    /// </summary>
    /// <returns>The type of the method.</returns>
    public override PodStorageMethodType GetMethodType() { return PodStorageMethodType.Utility; }
    /// <summary>
    /// Returns a name identifying the method.
    /// </summary>
    /// <returns>The name of the method.</returns>
    public override string GetMethodName()
    {
        if (!string.IsNullOrWhiteSpace(Name)) return Name;
        var name = "psU";
        name += UtilityConfig.WeightSpeed.ToString(IOConstants.EXPORT_FORMAT_SHORTER, IOConstants.FORMATTER);
        name += UtilityConfig.WeightUtility.ToString(IOConstants.EXPORT_FORMAT_SHORTER, IOConstants.FORMATTER);
        name += UtilityConfig.RankCorridor.ToString(IOConstants.EXPORT_FORMAT_SHORTER, IOConstants.FORMATTER);
        return name;
    }
    /// <summary>
    /// The fractional amount of storage locations considered to be the most popular locations (by their distance to the output-stations).
    /// </summary>
    public readonly PodUtilityConfiguration UtilityConfig = new();
}
/// <summary>
/// The configuration for the corresponding method.
/// </summary>
public class TurnoverPodStorageConfiguration : PodStorageConfiguration
{
    /// <summary>
    /// Returns the type of the corresponding method this configuration belongs to.
    /// </summary>
    /// <returns>The type of the method.</returns>
    public override PodStorageMethodType GetMethodType() { return PodStorageMethodType.Turnover; }
    /// <summary>
    /// Returns a name identifying the method.
    /// </summary>
    /// <returns>The name of the method.</returns>
    public override string GetMethodName()
    {
        if (!string.IsNullOrWhiteSpace(Name)) return Name;
        var name = "psT" + ClassBorders.Count(c => c == IOConstants.DELIMITER_LIST);
        name += StorageLocationClassRule switch
        {
            TurnoverPodStorageLocationClassRule.OutputStationDistanceEuclidean => "e",
            TurnoverPodStorageLocationClassRule.OutputStationDistanceManhattan => "m",
            TurnoverPodStorageLocationClassRule.OutputStationDistanceShortestPath => "s",
            TurnoverPodStorageLocationClassRule.OutputStationDistanceShortestTime => "t",
            _ => throw new ArgumentException("Unexpected argument!")
        };
        name += PodDisposeRule switch
        {
            TurnoverPodStorageLocationDisposeRule.NearestEuclid => "e",
            TurnoverPodStorageLocationDisposeRule.NearestManhattan => "m",
            TurnoverPodStorageLocationDisposeRule.NearestShortestPath => "s",
            TurnoverPodStorageLocationDisposeRule.NearestShortestTime => "d",
            TurnoverPodStorageLocationDisposeRule.OStationNearestEuclid => "n",
            TurnoverPodStorageLocationDisposeRule.OStationNearestManhattan => "t",
            TurnoverPodStorageLocationDisposeRule.OStationNearestShortestPath => "p",
            TurnoverPodStorageLocationDisposeRule.OStationNearestShortestTime => "a",
            TurnoverPodStorageLocationDisposeRule.Random => "r",
            _ => throw new ArgumentException("Unexpected argument!")
        };
        return name;
    }
    /// <summary>
    /// The fraction of the storage used for A items.
    /// </summary>
    public readonly string ClassBorders = "0.1" + IOConstants.DELIMITER_LIST + "0.3" + IOConstants.DELIMITER_LIST + "1.0";
    /// <summary>
    /// The time between two subsequent runs of the re-allocation of item-descriptions and pods to the storage classes.
    /// </summary>
    public double ReallocationDelay = 0.0;
    /// <summary>
    /// The number of orders between two subsequent runs of the re-allocation of item-descriptions and pods to the storage classes.
    /// </summary>
    public readonly int ReallocationOrderCount = 0;
    /// <summary>
    /// Indicates which rule to use to assign the storage locations to the different classes.
    /// </summary>
    public readonly TurnoverPodStorageLocationClassRule StorageLocationClassRule = TurnoverPodStorageLocationClassRule.OutputStationDistanceShortestTime;
    /// <summary>
    /// Indicates how a free storage location is selected from all free storage locations of a class.
    /// </summary>
    public readonly TurnoverPodStorageLocationDisposeRule PodDisposeRule = TurnoverPodStorageLocationDisposeRule.NearestShortestTime;
    /// <summary>
    /// Checks whether the pod storage configuration is valid.
    /// </summary>
    /// <param name="errorMessage">A message describing the error if the configuration is not valid.</param>
    /// <returns>Indicates whether the pod storage configuration is valid.</returns>
    public override bool AttributesAreValid(out String errorMessage)
    {
        if (ReallocationDelay < 0)
        {
            errorMessage = "Problem with pod storage configuration: ReallocationDelay has to be >= 0";
            return false;
        }
        if (ReallocationOrderCount < 0)
        {
            errorMessage = "Problem with pod storage configuration: ReallocationOrderCount has to be >= 0";
            return false;
        }
        errorMessage = "";
        return true;
    }
}

#endregion