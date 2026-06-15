using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAWSimO.Core.Bots;
using RAWSimO.Core.Control;
using RAWSimO.Core.Waypoints;
using RAWSimO.Rendering2D;
using RAWSimO.WebServer.Shared.SimulationHost.Types;
using RAWSimO.WebServer.Shared.SimulationHost.Services;
using RAWSimO.WebServer.SimulationHost.Services;

namespace RAWSimO.WebServer.SimulationHost.Controllers;

[ApiController]
[Route("simulation/[action]")]
public sealed class SimulationHostController(ISimulationHostService hostService, ISimulationStreamService streamService, IInstanceProvider instanceProvider, SimulationHostServices concreteService, ISimBackendOptions backendOptions)
    : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Health()
    {
        return Ok(await hostService.Health());
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetSimulationStatus()
    {
        return Ok(await hostService.GetStatus());
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> StartSimulation([FromBody] StartRequest request)
    {
        return Ok(await hostService.StartSimulation(request));
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> StopSimulation()
    {
        return Ok(await hostService.EndSimulation());
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadStatistics([FromQuery(Name = "output_dir_name")] string outputDirName)
    {
        var response = await hostService.DownloadStatistics(new DownloadStatisticsRequest(outputDirName));
        return File(response.ZipBytes, "application/zip", response.FileName);
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> PauseSimulation()
    {
        return Ok(await hostService.PauseSimulation());
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> ResumeSimulation()
    {
        return Ok(await hostService.ResumeSimulation());
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> GetLatestFrame([FromBody] RenderFrameRequest request)
    {
        return Ok(await hostService.GetLatestFrame(request));
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> AppendTasks([FromBody] AppendTasksRequest request)
    {
        return Ok(await hostService.AppendTasks(request));
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateRenderOptions([FromBody] UpdateRenderOptionsRequest request)
    {
        return Ok(await hostService.UpdateRenderOptions(request));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task StreamFrames(
        [FromQuery] int widthPx = 800,
        [FromQuery] int heightPx = 600,
        [FromQuery] int tierIndex = 0,
        [FromQuery] bool drawBots = true,
        [FromQuery] bool drawPods = true,
        [FromQuery] bool drawStations = true,
        [FromQuery] bool drawWaypoints = false)
    {
        await streamService.StreamFramesSse(
            Response,
            widthPx,
            heightPx,
            tierIndex,
            new RenderOptions
            {
                DrawBots = drawBots,
                DrawPods = drawPods,
                DrawStations = drawStations,
                DrawWaypoints = drawWaypoints
            },
            HttpContext.RequestAborted);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task StreamSimulationData()
    {
        await streamService.StreamSimulationDataSse(Response, HttpContext.RequestAborted);
    }

        public class RobotPoseDto
        {
            public int robot_id { get; set; }
            public double[]? position { get; set; }
            public double[]? rotation { get; set; }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task StreamPhysicalDataSse()
        {
            Response.Headers.Append("Content-Type", "text/event-stream");
            bool usePhysical = backendOptions.Backend == PhysicsBackend.External;

            while (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                if (usePhysical)
                {
                    var instance = instanceProvider.GetCurrentInstance();
                    if (instance != null)
                    {
                        var robots = new List<object>();
                        foreach (var bot in instance.Bots)
                        {
                            // Compute the immediate next waypoint from the path planner.
                            // Prefer NextWaypoint (set by BotMove.Act via setNextWaypoint),
                            // but fall back to reading Path.NextAction directly because
                            // setNextWaypoint can be blocked by GetSpeed() > 0 in physical mode.
                            var botNormal = bot as BotNormal;
                            double[] dest;

                            var nextWp = botNormal?.NextWaypoint;
                            if (nextWp != null)
                            {
                                dest = new[] { nextWp.X, nextWp.Y };
                            }
                            else if (botNormal?.Path != null && botNormal.Path.Count > 0)
                            {
                                var pathWp = instance.Controller.PathManager.GetWaypointByNodeId(botNormal.Path.NextAction.Node);
                                dest = pathWp != null
                                    ? new[] { pathWp.X, pathWp.Y }
                                    : new[] { bot.X, bot.Y };
                            }
                            else
                            {
                                dest = new[] { bot.X, bot.Y };
                            }

                            // Build the full remaining path for lookahead
                            List<double[]>? pathSegments = null;
                            if (botNormal?.Path != null && botNormal.Path.Count > 0)
                            {
                                pathSegments = new List<double[]>();
                                bool isFirst = true;
                                foreach (var action in botNormal.Path.Actions)
                                {
                                    if (isFirst) { isFirst = false; continue; }
                                    var wp = instance.Controller.PathManager.GetWaypointByNodeId(action.Node);
                                    if (wp != null)
                                        pathSegments.Add(new[] { wp.X, wp.Y });
                                }
                                if (pathSegments.Count == 0) pathSegments = null;
                            }

                            robots.Add(new
                            {
                                robot_id = bot.ID,
                                destination = dest,
                                position = bot.LastConfirmedPosition ?? new[] { bot.X, bot.Y, bot.Orientation },
                                path = pathSegments
                            });
                        }
                        var pods = new List<object>();
                        foreach (var pod in instance.Pods)
                        {
                            pods.Add(new
                            {
                                pod_id = pod.ID,
                                position = new[] { pod.X, pod.Y, pod.Orientation }
                            });
                        }
                        var payload = new { robots, pods };
                        await Response.WriteAsync($"data: {System.Text.Json.JsonSerializer.Serialize(payload)}\n\n");
                    }
                }
                else
                {
                    await Response.WriteAsync(":\n\n");
                }
                await Response.Body.FlushAsync();
                await Task.Delay(100);
            }
        }

        [HttpPost]
        [AllowAnonymous]
        public IActionResult UpdatePhysicalPose([FromBody] RobotPoseDto pose)
        {
            bool usePhysical = backendOptions.Backend == PhysicsBackend.External;
            if (usePhysical)
            {
                var instance = instanceProvider.GetCurrentInstance();
                if (instance != null)
                {
                    lock(instance)
                    {
                        if (pose.robot_id >= 0)
                        {
                            var bot = instance.Bots.FirstOrDefault(b => b.ID == pose.robot_id);
                            if (bot != null && pose.position != null && pose.position.Length >= 2)
                            {
                                double newO = pose.rotation != null && pose.rotation.Length >= 4 ? pose.rotation[3] : bot.Orientation;
                                var tier = instance.Compound.BotCurrentTier.ContainsKey(bot) ? instance.Compound.BotCurrentTier[bot] : null;
                                if (tier != null)
                                {
                                    tier.MoveBotOverride(bot, pose.position[0], pose.position[1]);
                                    bot.SetPhysicalOrientation(newO);
                                }
                                else
                                {
                                    bot.SetPhysicalState(pose.position[0], pose.position[1], newO);
                                }
                                bot.LastConfirmedPosition = new[] { bot.X, bot.Y, bot.Orientation };
                                bot.LastPhysicalUpdateTime = DateTime.UtcNow;

                                // Zero velocity so BotMove.Act's setNextWaypoint succeeds
                                bot.ZeroVelocity();

                                // Determine the current target waypoint (NextWaypoint or from path)
                                var botNormal = bot as BotNormal;
                                Waypoint targetWp = botNormal?.NextWaypoint;
                                if (targetWp == null && botNormal?.Path != null && botNormal.Path.Count > 0)
                                {
                                    targetWp = instance.Controller.PathManager.GetWaypointByNodeId(botNormal.Path.NextAction.Node);
                                }

                                if (targetWp != null)
                                {
                                    double wpDist = Math.Sqrt(
                                        Math.Pow(bot.X - targetWp.X, 2) +
                                        Math.Pow(bot.Y - targetWp.Y, 2));
                                    if (wpDist < 0.5)
                                    {
                                        // Bot reached the waypoint
                                        bot.CurrentWaypoint = targetWp;

                                        // Advance the path directly so SSE sees the next
                                        // segment immediately, without waiting for BotMove.Act.
                                        if (botNormal?.Path != null && botNormal.Path.Count > 0)
                                        {
                                            var nextActionNode = instance.Controller.PathManager.GetWaypointByNodeId(botNormal.Path.NextAction.Node);
                                            if (nextActionNode != null && nextActionNode.ID == targetWp.ID)
                                            {
                                                botNormal.Path.RemoveFirstAction();
                                                while (botNormal.Path.Count > 0 && !botNormal.Path.NextAction.StopAtNode)
                                                    botNormal.Path.RemoveFirstAction();
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return Ok();
        }

    [HttpPost]
    [AllowAnonymous]
    public IActionResult SetSpeed([FromBody] SetSpeedRequest request)
    {
        return Ok(concreteService.SetSpeed(request));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetTestMetadata()
    {
        return Ok(concreteService.GetTestMetadata());
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task StreamTestMetadata()
    {
        await concreteService.StreamTestMetadataSse(Response, HttpContext.RequestAborted);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetSimBackend()
    {
        // Query the active simulation data-source backend ("internal" or "external").
        return Ok(new { backend = backendOptions.Backend.ToString().ToLowerInvariant() });
    }

    [HttpPost]
    [AllowAnonymous]
    public IActionResult SetSimBackend([FromQuery] string backend)
    {
        // Switch the simulation data-source backend at runtime: "internal" (pure RAWSim-O)
        // or "external" (Isaac Lab physical sim). Resolved by both Core (BotNormal) and
        // the physical SSE/pose endpoints via the shared SimBackendOptions.
        if (Enum.TryParse<PhysicsBackend>(backend, ignoreCase: true, out var parsed))
        {
            backendOptions.Backend = parsed;
            return Ok(new { backend = parsed.ToString().ToLowerInvariant() });
        }
        return BadRequest(new { error = $"Unknown backend '{backend}'. Use 'internal' or 'external'." });
    }
}
