using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAWSimO.Rendering2D;
using RAWSimO.WebServer.Shared.SimulationHost.Types;
using RAWSimO.WebServer.Shared.SimulationHost.Services;
using RAWSimO.WebServer.SimulationHost.Services;

namespace RAWSimO.WebServer.SimulationHost.Controllers;

[ApiController]
[Route("simulation/[action]")]
public sealed class SimulationHostController(ISimulationHostService hostService, ISimulationStreamService streamService, IInstanceProvider instanceProvider)
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
            bool usePhysical = Environment.GetEnvironmentVariable("USE_RAWSIMO_PHYSICAL")?.ToLower() == "true";

            while (!HttpContext.RequestAborted.IsCancellationRequested)
            {
                if (usePhysical)
                {
                    var instance = instanceProvider.GetCurrentInstance();
                    if (instance != null)
                    {
                        // Send a single batch event with all robots and pods
                        var robots = new List<object>();
                        foreach (var bot in instance.Bots)
                        {
                            robots.Add(new
                            {
                                robot_id = bot.ID,
                                destination = new[] { bot.GetCurrentTargetX(), bot.GetCurrentTargetY() },
                                position = bot.LastConfirmedPosition ?? new[] { bot.X, bot.Y, bot.Orientation }
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
            bool usePhysical = Environment.GetEnvironmentVariable("USE_RAWSIMO_PHYSICAL")?.ToLower() == "true";
            if (usePhysical)
            {
                var instance = instanceProvider.GetCurrentInstance();
                if (instance != null)
                {
                    lock(instance)
                    {
                        // Handle robot pose update
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
                            }
                        }
                    }
                }
            }
            return Ok();
        }

}