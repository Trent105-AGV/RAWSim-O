using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RAWSimO.WebServer.Shared.SimulationHost.Types;
using RAWSimO.WebServer.Shared.SimulationHost.Services;

namespace RAWSimO.WebServer.SimulationHost.Controllers;

[ApiController]
[Route("simulation/[action]")]
public sealed class SimulationHostController : ControllerBase
{
	private readonly ISimulationHostService _hostService;

	public SimulationHostController(ISimulationHostService hostService)
	{
		_hostService = hostService;
	}

	[HttpGet]
	[AllowAnonymous]
	public async Task<IActionResult> Health()
	{
		return Ok(await _hostService.Health());
	}

	[HttpGet]
	[AllowAnonymous]
	public async Task<IActionResult> GetSimulationStatus()
	{
		return Ok(await _hostService.GetStatus());
	}

	[HttpPost]
	public async Task<IActionResult> StartSimulation([FromBody] StartRequest request)
	{
		return Ok(await _hostService.StartSimulation(request));
	}

	[HttpPost]
	public async Task<IActionResult> StopSimulation()
	{
		return Ok(await _hostService.EndSimulation());
	}

	[HttpPost]
	public async Task<IActionResult> PauseSimulation()
	{
		return Ok(await _hostService.PauseSimulation());
	}

	[HttpPost]
	public async Task<IActionResult> ResumeSimulation()
	{
		return Ok(await _hostService.ResumeSimulation());
	}

	[HttpPost]
	public async Task<IActionResult> GetLatestFrame([FromBody] RenderFrameRequest request)
	{
		return Ok(await _hostService.GetLatestFrame(request));
	}
}