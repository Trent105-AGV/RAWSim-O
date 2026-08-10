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

    // --- Visualization speed scales (external/physical mode only) ---------------------------
    // Isaac Lab drives bots this many times faster than their realistic Physics, for fast
    // visualization (see Isaac's motion_backends.py: VEL/ACCEL/DECEL_VIZ_SCALE and
    // ROTATION_SPEED = 3*pi rad/s). The reservation table MUST time bot segments at these scaled
    // speeds, otherwise its cooperative space-time plan does not match the real (fast) motion and
    // bots pile up. These MUST stay in sync with motion_backends.py.
    /// <summary>== Isaac <c>VEL_VIZ_SCALE</c>.</summary>
    public const double VizVelocityScale = 15.0;
    /// <summary>== Isaac <c>ACCEL_VIZ_SCALE</c>.</summary>
    public const double VizAccelScale = 100.0;
    /// <summary>== Isaac <c>DECEL_VIZ_SCALE</c>.</summary>
    public const double VizDecelScale = 150.0;
    /// <summary>Time (s) for a full 2*pi turn as Isaac drives it: 2*pi / (3*pi rad/s) = 2/3.</summary>
    public const double VizTurnTime = 2.0 / 3.0;

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
