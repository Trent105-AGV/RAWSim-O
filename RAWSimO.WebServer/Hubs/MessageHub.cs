using Microsoft.AspNetCore.SignalR;
using RAWSimO.WebServer.Shared.Hubs;

namespace RAWSimO.WebServer.Hubs;

public class MessageHub(ILogger<MessageHub> logger) : Hub<IMessageClient>
{
    // ReSharper disable once UnusedMember.Local
    private readonly ILogger<MessageHub> _logger = logger;

    public override Task OnConnectedAsync()
    {
        _logger.LogInformation("SignalR connected: {ConnectionId}", Context.ConnectionId);
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is null)
            _logger.LogInformation("SignalR disconnected: {ConnectionId}", Context.ConnectionId);
        else
            _logger.LogWarning(exception, "SignalR disconnected: {ConnectionId}", Context.ConnectionId);

        return base.OnDisconnectedAsync(exception);
    }
}
