namespace RAWSimO.Core.Info;

/// <summary>
/// The more basic interface for getting information about an immovable object.
/// </summary>
public interface IImmovableObjectInfo : IGeneralObjectInfo
{
    /// <summary>
    /// The length of the objects' area. (Corresponds to the x-axis)
    /// </summary>
    /// <returns>The length of the objects' area.</returns>
    double GetInfoLength();
    /// <summary>
    /// The width of the objects' area. (Corresponds to the y-axis)
    /// </summary>
    /// <returns>The width of the objects' area.</returns>
    double GetInfoWidth();
}