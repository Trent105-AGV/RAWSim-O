using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAWSimO.Rendering2D;
using RAWSimO.WebServer.Shared.SimulationHost.Types;
using RAWSimO.WebServer.Shared.SimulationHost.Services;
using RAWSimO.WebServer.SimulationHost.Services;

namespace RAWSimO.WebServer.SimulationHost.Controllers;

[ApiController]
[Route("simulation/[action]")]
public sealed class SimulationHostController(ISimulationHostService hostService, ISimulationStreamService streamService)
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
}