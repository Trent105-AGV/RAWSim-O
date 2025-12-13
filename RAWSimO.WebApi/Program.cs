using Microsoft.AspNetCore.Http.HttpResults;
using RAWSimO.Rendering2D;
using RAWSimO.WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<SimulationHost>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { ok = true }));

app.MapGet("/simulation/status", (SimulationHost host) =>
{
    var resp = new StatusResponse(host.IsRunning, host.SimTime, host.LastError);
    return Results.Ok(resp);
});

app.MapPost("/simulation/start",
    async Task<Results<Ok, BadRequest<string>, Conflict<string>>> (SimulationHost host, StartRequest req) =>
    {
        if (string.IsNullOrWhiteSpace(req.Instance) || string.IsNullOrWhiteSpace(req.Setting) ||
            string.IsNullOrWhiteSpace(req.ControlConfig) ||
            string.IsNullOrWhiteSpace(req.StatisticsDir))
            return TypedResults.BadRequest("Instance/Setting/ControlConfig/StatisticsDir are required");

        if (!host.TryStart(req, out var error))
            return TypedResults.Conflict(error ?? "Failed to start");

        return TypedResults.Ok();
    });

app.MapPost("/simulation/stop", (SimulationHost host) =>
{
    host.Stop();
    return Results.Ok();
});

app.MapGet("/render/frame", (SimulationHost host, int w = 800, int h = 600, int tier = 0,
    bool bots = true, bool pods = true, bool stations = true, bool waypoints = false) =>
{
    var options = new RenderOptions
    {
        DrawBots = bots,
        DrawPods = pods,
        DrawStations = stations,
        DrawWaypoints = waypoints
    };

    var frame = host.GetLatestFrame(w, h, tier, options);
    return Results.Ok(frame);
});

app.MapGet("/render/stream", async (HttpContext ctx, SimulationHost host, int w = 800, int h = 600, int tier = 0,
    bool bots = true, bool pods = true, bool stations = true, bool waypoints = false) =>
{
    var options = new RenderOptions
    {
        DrawBots = bots,
        DrawPods = pods,
        DrawStations = stations,
        DrawWaypoints = waypoints
    };

    await host.StreamFramesSse(ctx.Response, w, h, tier, options, ctx.RequestAborted);
});

app.Run();
