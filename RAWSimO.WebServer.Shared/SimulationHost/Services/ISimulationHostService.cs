using MagicOnion;
using RAWSimO.WebServer.Shared.SimulationHost.Types;

namespace RAWSimO.WebServer.Shared.SimulationHost.Services;

/// <summary>
/// SimulationHost Service Interface using MagicOnion for RPC communication
/// </summary>
public interface ISimulationHostService : IService<ISimulationHostService>
{
    /// <summary>
    /// Health check endpoint
    /// </summary>
    UnaryResult<bool> Health();

    /// <summary>
    /// Get current simulation status
    /// </summary>
    UnaryResult<StatusResponse> GetStatus();

    /// <summary>
    /// Start a new simulation instance
    /// </summary>
    UnaryResult<StartResponse> StartSimulation(StartRequest request);

    /// <summary>
    /// Stop the running simulation
    /// </summary>
    UnaryResult<bool> EndSimulation();

    /// <summary>
    /// Pause the running simulation
    /// </summary>
    UnaryResult<bool> PauseSimulation();

    /// <summary>
    /// Resume the paused simulation
    /// </summary>
    UnaryResult<bool> ResumeSimulation();

    /// <summary>
    /// Get the latest rendered frame
    /// </summary>
    UnaryResult<RenderFrameResponse> GetLatestFrame(RenderFrameRequest request);
}
