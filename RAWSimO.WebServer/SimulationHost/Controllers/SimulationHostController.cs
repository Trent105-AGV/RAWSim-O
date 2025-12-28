using System.IO.Compression;
using System.Text.RegularExpressions;
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
	private const string StatisticsRootDirectory = "/app/out";
	private static readonly Regex OutputDirNamePattern = new("^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

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
	public IActionResult DownloadStatistics([FromQuery] string outputDirName)
	{
		if (string.IsNullOrWhiteSpace(outputDirName))
			return BadRequest("outputDirName is required");
		if (!OutputDirNamePattern.IsMatch(outputDirName))
			return BadRequest("invalid outputDirName");

		var baseFullPath = Path.GetFullPath(StatisticsRootDirectory);
		if (!baseFullPath.EndsWith(Path.DirectorySeparatorChar))
			baseFullPath += Path.DirectorySeparatorChar;

		var targetDir = Path.GetFullPath(Path.Combine(StatisticsRootDirectory, outputDirName));
		if (!targetDir.StartsWith(baseFullPath, StringComparison.Ordinal))
			return BadRequest("invalid outputDirName");
		if (!Directory.Exists(targetDir))
			return NotFound("statistics directory not found");

		Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "RAWSimO.WebServer", "downloads"));
		var zipPath = Path.Combine(
			Path.GetTempPath(),
			"RAWSimO.WebServer",
			"downloads",
			$"{outputDirName}-{Guid.NewGuid():N}.zip");

		ZipFile.CreateFromDirectory(targetDir, zipPath, CompressionLevel.Fastest, includeBaseDirectory: false);

		var stream = new FileStream(
			zipPath,
			FileMode.Open,
			FileAccess.Read,
			FileShare.Read,
			bufferSize: 64 * 1024,
			options: FileOptions.Asynchronous | FileOptions.DeleteOnClose);

		return File(stream, "application/zip", $"{outputDirName}.zip");
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