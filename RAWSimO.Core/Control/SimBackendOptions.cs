using System;
using System.Threading;

namespace RAWSimO.Core.Control;

/// <summary>
/// Selects which simulation data-source / motion backend drives robot kinematics.
/// </summary>
/// <remarks>
/// <para><b>Internal</b>: RAWSim-O runs its own kinematic simulation and is the
/// authority for robot/pod positions (the <c>StreamSimulationData</c> stream).</para>
/// <para><b>External</b>: an external physics engine (e.g. Isaac Lab) owns kinematics;
/// RAWSim-O keeps task/path planning and ingests integrated poses over the HTTP
/// endpoints <c>StreamPhysicalDataSse</c> / <c>UpdatePhysicalPose</c>.</para>
/// <para>This is the single switch point for the simulation data-source backend.</para>
/// </remarks>
public enum PhysicsBackend
{
    /// <summary>RAWSim-O's built-in kinematic simulation is authoritative.</summary>
    Internal,

    /// <summary>An external physics engine (e.g. Isaac Lab) drives kinematics.</summary>
    External,
}

/// <summary>
/// Single source of truth for the active <see cref="PhysicsBackend"/>. Resolved once
/// at first use from the <c>USE_RAWSIMO_PHYSICAL</c> environment variable and settable
/// at runtime (thread-safe). Core code (which has no DI container) reads this static;
/// the WebServer layer injects <see cref="ISimBackendOptions"/> which proxies it.
/// </summary>
public static class SimBackendOptions
{
    private static volatile PhysicsBackend _current = ResolveFromEnvironment();
    private static readonly object _lock = new();

    /// <summary>The currently active simulation backend.</summary>
    public static PhysicsBackend Current
    {
        get => _current;
        set
        {
            lock (_lock)
            {
                _current = value;
            }
        }
    }

    /// <summary>True when the external/physical (Isaac Lab) backend is active.</summary>
    public static bool IsExternal => _current == PhysicsBackend.External;

    private static PhysicsBackend ResolveFromEnvironment()
    {
        string raw = Environment.GetEnvironmentVariable("USE_RAWSIMO_PHYSICAL");
        return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            ? PhysicsBackend.External
            : PhysicsBackend.Internal;
    }
}

/// <summary>
/// DI-friendly accessor over the process-global <see cref="SimBackendOptions"/>. Allows
/// the WebServer layer to query and (at runtime) switch the simulation backend.
/// </summary>
public interface ISimBackendOptions
{
    /// <summary>The currently active simulation backend.</summary>
    PhysicsBackend Backend { get; set; }
}

/// <summary>
/// Default <see cref="ISimBackendOptions"/> implementation backed by the static
/// <see cref="SimBackendOptions"/>. Registered as a singleton so Core and WebServer
/// observe the same value.
/// </summary>
public sealed class SimBackendOptionsAccessor : ISimBackendOptions
{
    /// <inheritdoc />
    public PhysicsBackend Backend
    {
        get => SimBackendOptions.Current;
        set => SimBackendOptions.Current = value;
    }
}
